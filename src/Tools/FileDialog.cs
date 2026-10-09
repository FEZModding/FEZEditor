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
        var context = new DialogContext(callback, options.Filters);
        uint props = 0;
        var shown = false;

        try
        {
            props = SDL.SDL_CreateProperties();
            if (context.FilterCount > 0)
            {
                SDL.SDL_SetPointerProperty(props, SDL.SDL_PROP_FILE_DIALOG_FILTERS_POINTER, context.FilterPointer);
                SDL.SDL_SetNumberProperty(props, SDL.SDL_PROP_FILE_DIALOG_NFILTERS_NUMBER, context.FilterCount);
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
                context.UserData,
                props);
            shown = true;
        }
        finally
        {
            if (props != 0)
            {
                SDL.SDL_DestroyProperties(props);
            }

            if (!shown)
            {
                context.Dispose();
            }
        }
    }

    private static unsafe SDL.SDL_DialogFileFilter[] ConvertFilters(Filter[]? filters)
    {
        if (filters == null || filters.Length == 0)
        {
            return [];
        }

        var nativeFilters = new SDL.SDL_DialogFileFilter[filters.Length];
        try
        {
            for (var i = 0; i < filters.Length; i++)
            {
                nativeFilters[i].name = (byte*)Marshal.StringToCoTaskMemUTF8(filters[i].Name);
                nativeFilters[i].pattern = (byte*)Marshal.StringToCoTaskMemUTF8(filters[i].Pattern);
            }

            return nativeFilters;
        }
        catch
        {
            FreeFilters(nativeFilters);
            throw;
        }
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

    private sealed class DialogContext : IDisposable
    {
        public IntPtr UserData => GCHandle.ToIntPtr(_handle);

        public SDL.SDL_DialogFileCallback Callback { get; }

        public int FilterCount => _nativeFilters.Length;

        public IntPtr FilterPointer => _filterHandle.IsAllocated ? _filterHandle.AddrOfPinnedObject() : IntPtr.Zero;

        private readonly Action<string[]> _userCallback;

        private readonly SDL.SDL_DialogFileFilter[] _nativeFilters;

        private GCHandle _filterHandle;

        private GCHandle _handle;

        private int _disposed;

        public DialogContext(Action<string[]> userCallback, Filter[]? filters)
        {
            _userCallback = userCallback;
            Callback = OnDialogComplete;
            _nativeFilters = ConvertFilters(filters);
            try
            {
                if (_nativeFilters.Length > 0)
                {
                    // SDL keeps the filters until the asynchronous dialog callback.
                    _filterHandle = GCHandle.Alloc(_nativeFilters, GCHandleType.Pinned);
                }

                _handle = GCHandle.Alloc(this);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            FreeFilters(_nativeFilters);
            if (_filterHandle.IsAllocated)
            {
                _filterHandle.Free();
            }

            if (_handle.IsAllocated)
            {
                _handle.Free();
            }
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
                Dispose();
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
