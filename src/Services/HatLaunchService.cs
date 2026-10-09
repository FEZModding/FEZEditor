using System.Diagnostics;
using FezEditor.Components;
using FezEditor.Structure;
using FezEditor.Tools;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Serilog;

namespace FezEditor.Services;

[UsedImplicitly]
public class HatLaunchService : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<HatLaunchService>();

    private string ModSourcePath => _resources.ModResolution?.ModRootDirectory.FullName ?? _resources.RootPath;

    private readonly AppStorageService _storage;

    private readonly EditorService _editors;

    private readonly ResourceService _resources;

    private readonly IContentManager _content;

    private readonly HatModMount _modMount = new();

    private Process? _hatProcess;

    public HatLaunchService(Game game)
    {
        _storage = game.GetService<AppStorageService>();
        _editors = game.GetService<EditorService>();
        _resources = game.GetService<ResourceService>();
        _content = game.GetService<ContentService>().Global;
        _resources.ProviderReset += OnProviderReset;
    }

    public HatAvailability GetAvailability(EddyEditor editor)
    {
        if (_hatProcess != null)
        {
            return new HatAvailability.Unavailable("FEZ is already running.");
        }

        if (string.IsNullOrWhiteSpace(_storage.HatLauncherPath))
        {
            return new HatAvailability.Unavailable("Locate the FEZ executable with HAT 3 installed before launching levels.");
        }

        if (!File.Exists(_storage.HatLauncherPath))
        {
            return new HatAvailability.Unavailable($"FEZ executable does not exist: {_storage.HatLauncherPath}");
        }

        if (!_editors.TryGetEditorPath(editor, out var path))
        {
            return new HatAvailability.Unavailable("Level is not tracked by the editor.");
        }

        if (_resources.IsReadonlyPath(path))
        {
            return new HatAvailability.Unavailable("Readonly levels cannot be launched.");
        }

        if (!EditorService.TryGetLevelName(path, out _))
        {
            return new HatAvailability.Unavailable("Move this level into Levels before launching.");
        }

        return new HatAvailability.Available();
    }

    public void Launch(EddyEditor editor)
    {
        var availability = GetAvailability(editor);
        if (availability is HatAvailability.Unavailable unavailable)
        {
            Logger.Error("Unable to launch FEZ: {Reason}", unavailable.Reason);
            return;
        }

        if (!_editors.TryGetEditorPath(editor, out var path) || !EditorService.TryGetLevelName(path, out var levelName))
        {
            Logger.Error("Move this level into Levels with a valid name before launching.");
            return;
        }

        _editors.SaveEditorChanges(editor);

        try
        {
            var launcherPath = _storage.HatLauncherPath;
            _modMount.Stage(launcherPath, ModSourcePath, _resources.ModResolution != null, _content);

            var startInfo = new ProcessStartInfo
            {
                FileName = launcherPath,
                WorkingDirectory = Path.GetDirectoryName(launcherPath) ?? string.Empty,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("--level");
            startInfo.ArgumentList.Add(levelName);

            _hatProcess = new Process { StartInfo = startInfo };
            _hatProcess.EnableRaisingEvents = true;
            _hatProcess.Exited += (_, _) =>
            {
                RemovePreviousMod();
                _hatProcess = null;
                Logger.Information("FEZ closed");
            };
            if (!_hatProcess.Start())
            {
                _hatProcess = null;
            }

            Logger.Information("Launched FEZ - {Launcher} --level {Level}", launcherPath, levelName);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Unable to launch FEZ");
        }
    }

    private void OnProviderReset()
    {
        if (_hatProcess == null)
        {
            RemovePreviousMod();
        }
    }

    private void RemovePreviousMod()
    {
        try
        {
            _modMount.RemoveIfDifferent(ModSourcePath);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Unable to remove the previous playtest mod");
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _resources.ProviderReset -= OnProviderReset;
        if (_hatProcess is { HasExited: false })
        {
            if (_hatProcess.CloseMainWindow())
            {
                _hatProcess.Kill(entireProcessTree: true);
            }
        }

        _hatProcess?.Dispose();
    }
}
