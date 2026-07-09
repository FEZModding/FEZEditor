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

    private const string LevelsPrefix = "Levels/";

    private const string ModName = "FEZEditor";

    private const string MetadataAsset = "Metadata.xml";

    private readonly AppStorageService _storage;

    private readonly EditorService _editors;

    private readonly ResourceService _resources;

    private readonly IContentManager _content;

    private Process? _hatProcess;

    public HatLaunchService(Game game)
    {
        _storage = game.GetService<AppStorageService>();
        _editors = game.GetService<EditorService>();
        _resources = game.GetService<ResourceService>();
        _content = game.GetService<ContentService>().Global;
    }

    public HatAvailability GetAvailability(EddyEditor editor)
    {
        if (_hatProcess != null)
        {
            return new HatAvailability.Unavailable("HAT launcher is already running.");
        }

        if (string.IsNullOrWhiteSpace(_storage.HatLauncherPath))
        {
            return new HatAvailability.Unavailable("Locate HAT launcher before launching levels.");
        }

        if (!File.Exists(_storage.HatLauncherPath))
        {
            return new HatAvailability.Unavailable($"HAT launcher does not exist: {_storage.HatLauncherPath}");
        }

        if (!_editors.TryGetEditorPath(editor, out var path))
        {
            return new HatAvailability.Unavailable("Level is not tracked by the editor.");
        }

        if (_resources.IsReadonlyPath(path))
        {
            return new HatAvailability.Unavailable("Readonly levels cannot be launched.");
        }

        if (!TryGetLevelName(path, out _))
        {
            return new HatAvailability.Unavailable("Level asset must have a valid name.");
        }

        return new HatAvailability.Available();
    }

    public void Launch(EddyEditor editor)
    {
        var availability = GetAvailability(editor);
        if (availability is HatAvailability.Unavailable)
        {
            Logger.Error("Unable to launch HAT");
            return;
        }

        if (!_editors.TryGetEditorPath(editor, out var path) || !TryGetLevelName(path, out var levelName))
        {
            Logger.Error("Level asset must have a valid name.");
            return;
        }

        _editors.SaveEditorChanges(editor);

        try
        {
            var launcherPath = _storage.HatLauncherPath;
            StageLevelMod(launcherPath, path);

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
                _hatProcess = null;
                Logger.Information("HAT closed");
            };
            if (!_hatProcess.Start())
            {
                _hatProcess = null;
            }

            Logger.Information("Launched HAT - {Launcher} --level {Level}", launcherPath, levelName);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Unable to launch HAT");
        }
    }

    private void StageLevelMod(string launcherPath, string levelPath)
    {
        var launcherDirectory = Path.GetDirectoryName(launcherPath) ?? throw new DirectoryNotFoundException(launcherPath);
        var levelDirectory = Path.Combine(launcherDirectory, "Mods", ModName, "Assets", "Levels");
        var metadataFile = Path.Combine(launcherDirectory, "Mods", ModName, MetadataAsset);
        Directory.CreateDirectory(levelDirectory);

        #region Copy Metadata.xml

        {
            using var metadata = _content.LoadStream(MetadataAsset);
            using var output = File.Create(metadataFile);
            metadata.CopyTo(output);
        }

        #endregion

        #region Copy level file

        {
            var sourcePath = _resources.GetFullPath(levelPath);
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException(levelPath, sourcePath);
            }

            if (!TryGetLevelName(levelPath, out var levelName))
            {
                throw new InvalidOperationException("Level asset must have a valid name.");
            }

            var sourceDirectory = Path.GetDirectoryName(sourcePath)!;
            var sourceFileName = Path.GetFileName(sourcePath);
            var dotIndex = sourceFileName.IndexOf('.');
            var prefix = dotIndex >= 0 ? sourceFileName[..dotIndex] : Path.GetFileNameWithoutExtension(sourceFileName);

            foreach (var sourceFile in Directory.EnumerateFiles(sourceDirectory, prefix + ".*"))
            {
                var suffix = Path.GetFileName(sourceFile)[prefix.Length..];
                var destination = Path.Combine(levelDirectory, levelName + suffix);
                File.Copy(sourceFile, destination, overwrite: true);
            }
        }

        #endregion
    }

    private static bool TryGetLevelName(string path, out string levelName)
    {
        levelName = string.Empty;
        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith(LevelsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[LevelsPrefix.Length..];
        }

        levelName = Path.GetFileName(normalized);
        return !string.IsNullOrWhiteSpace(levelName);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
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