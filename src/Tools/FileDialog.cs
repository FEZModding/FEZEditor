using System.Runtime.InteropServices;
using SDL3;

namespace FezEditor.Tools;

public static class FileDialog
{
    public class Filter(string name = "", string pattern = "")
    {
        public string Name { get; init; } = name;
        public string Pattern { get; init; } = pattern;
    }

    public class Options
    {
        public Filter[] Filters { get; init; } = [];
        public string DefaultLocation { get; init; } = "";
        public bool AllowMultiple { get; init; }
        public string Title { get; init; } = "";
        public string AcceptButtonLabel { get; init; } = "";
        public string CancelButtonLabel { get; init; } = "";
    }

    public enum Type
    {
        OpenFile,
        SaveFile,
        OpenFolder
    }

    public static void Show(
        Type type,
        Action<string[]> callback,
        Options? options = null)
    {
        options ??= new Options();
        var context = new DialogContext(callback);
        var nativeFilters = ConvertFilters(options.Filters);
        var props = SDL.SDL_CreateProperties();

        try
        {
            if (nativeFilters.Length > 0)
            {
                unsafe
                {
                    fixed (SDL.SDL_DialogFileFilter* filterPtr = nativeFilters)
                    {
                        SDL.SDL_SetPointerProperty(props, SDL.SDL_PROP_FILE_DIALOG_FILTERS_POINTER, (IntPtr)filterPtr);
                    }
                }

                SDL.SDL_SetNumberProperty(props, SDL.SDL_PROP_FILE_DIALOG_NFILTERS_NUMBER, nativeFilters.Length);
            }

            if (!string.IsNullOrEmpty(options.DefaultLocation))
            {
                SDL.SDL_SetStringProperty(props, SDL.SDL_PROP_FILE_DIALOG_LOCATION_STRING, options.DefaultLocation);
            }

            if (options.AllowMultiple)
            {
                SDL.SDL_SetBooleanProperty(props, SDL.SDL_PROP_FILE_DIALOG_MANY_BOOLEAN, true);
            }

            if (!string.IsNullOrEmpty(options.Title))
            {
                SDL.SDL_SetStringProperty(props, SDL.SDL_PROP_FILE_DIALOG_TITLE_STRING, options.Title);
            }

            if (!string.IsNullOrEmpty(options.AcceptButtonLabel))
            {
                SDL.SDL_SetStringProperty(props, SDL.SDL_PROP_FILE_DIALOG_ACCEPT_STRING, options.AcceptButtonLabel);
            }

            if (!string.IsNullOrEmpty(options.CancelButtonLabel))
            {
                SDL.SDL_SetStringProperty(props, SDL.SDL_PROP_FILE_DIALOG_CANCEL_STRING, options.CancelButtonLabel);
            }

            SDL.SDL_ShowFileDialogWithProperties(
                type switch
                {
                    Type.OpenFile => SDL.SDL_FileDialogType.SDL_FILEDIALOG_OPENFILE,
                    Type.SaveFile => SDL.SDL_FileDialogType.SDL_FILEDIALOG_SAVEFILE,
                    Type.OpenFolder => SDL.SDL_FileDialogType.SDL_FILEDIALOG_OPENFOLDER,
                    _ => throw new ArgumentOutOfRangeException(nameof(type))
                },
                context.Callback,
                GCHandle.ToIntPtr(context.Handle),
                props);
        }
        finally
        {
            SDL.SDL_DestroyProperties(props);
            FreeFilters(nativeFilters);
        }
    }

    private static unsafe SDL.SDL_DialogFileFilter[] ConvertFilters(Filter[]? filters)
    {
        if (filters == null || filters.Length == 0)
        {
            return [];
        }

        var nativeFilters = new SDL.SDL_DialogFileFilter[filters.Length];
        for (var i = 0; i < filters.Length; i++)
        {
            nativeFilters[i] = new SDL.SDL_DialogFileFilter
            {
                name = (byte*)Marshal.StringToCoTaskMemUTF8(filters[i].Name),
                pattern = (byte*)Marshal.StringToCoTaskMemUTF8(filters[i].Pattern)
            };
        }

        return nativeFilters;
    }

    private static unsafe void FreeFilters(SDL.SDL_DialogFileFilter[] filters)
    {
        foreach (var filter in filters)
        {
            if (filter.name != null)
            {
                Marshal.FreeCoTaskMem((IntPtr)filter.name);
            }

            if (filter.pattern != null)
            {
                Marshal.FreeCoTaskMem((IntPtr)filter.pattern);
            }
        }
    }

    private class DialogContext
    {
        public GCHandle Handle { get; }

        public SDL.SDL_DialogFileCallback Callback { get; }

        private readonly Action<string[]> _userCallback;

        public DialogContext(Action<string[]> userCallback)
        {
            _userCallback = userCallback;
            Callback = OnDialogComplete;
            Handle = GCHandle.Alloc(this);
        }

        private void OnDialogComplete(IntPtr userdata, IntPtr filelist, int filter)
        {
            try
            {
                var result = ParseResult(filelist);
                if (result.Length > 0)
                {
                    _userCallback(result);
                }
            }
            finally
            {
                var handle = GCHandle.FromIntPtr(userdata);
                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }
        }

        private static string[] ParseResult(IntPtr filelist)
        {
            // Check if user cancelled (filelist will be null)
            if (filelist == IntPtr.Zero)
            {
                return Array.Empty<string>();
            }

            // Read the array of C strings
            var files = new List<string>();
            var i = 0;

            while (true)
            {
                // Read pointer at offset i
                var stringPtr = Marshal.ReadIntPtr(filelist, i * IntPtr.Size);

                // Null pointer marks end of array
                if (stringPtr == IntPtr.Zero)
                {
                    break;
                }

                // Convert C string to .NET string
                var filename = Marshal.PtrToStringUTF8(stringPtr);
                if (filename != null)
                {
                    files.Add(filename);
                }

                i++;
            }

            return files.ToArray();
        }
    }
}