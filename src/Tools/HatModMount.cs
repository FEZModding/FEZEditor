namespace FezEditor.Tools;

internal sealed class HatModMount
{
    private const string DirectoryName = "FEZEditor.Playtest";

    private const string MarkerName = ".fez-editor-playtest";

    private const string MarkerContents = "Temporary mod managed by FEZEditor";

    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private string? _directory;

    private string? _source;

    public void Stage(string launcherPath, string sourcePath, bool isMod, IContentManager content)
    {
        var source = GetDirectoryPath(sourcePath);
        var launcherDirectory = Path.GetDirectoryName(Path.GetFullPath(launcherPath))!;
        var modsDirectory = Path.Combine(launcherDirectory, "Mods");
        var destination = Path.Combine(modsDirectory, DirectoryName);

        if (isMod && IsMounted(source, modsDirectory))
        {
            if (string.Equals(source, GetDirectoryPath(destination), PathComparison))
            {
                _directory = null;
                _source = null;
                return;
            }

            RemoveIfDifferent(string.Empty);
            var marker = Path.Combine(destination, MarkerName);
            if (File.Exists(marker) && File.ReadAllText(marker) == MarkerContents)
            {
                RemoveTemporaryDirectory(destination);
            }

            return;
        }

        if (IsWithin(destination, source) || IsWithin(source, destination))
        {
            throw new IOException("The temporary playtest directory overlaps the edited mod.");
        }

        if (_directory != null && !string.Equals(_directory, destination, PathComparison))
        {
            RemoveIfDifferent(string.Empty);
        }

        RemoveIfDifferent(source);
        RemoveTemporaryDirectory(destination);
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(destination, MarkerName), MarkerContents);
        _directory = destination;
        _source = source;

        CopyDirectory(source, isMod ? destination : Path.Combine(destination, "Assets"));
        if (!isMod)
        {
            using var metadata = content.LoadStream("Metadata.xml");
            using var output = File.Create(Path.Combine(destination, "Metadata.xml"));
            metadata.CopyTo(output);
        }
    }

    public void RemoveIfDifferent(string sourcePath)
    {
        if (_directory == null || string.Equals(_source, GetDirectoryPath(sourcePath), PathComparison))
        {
            return;
        }

        RemoveTemporaryDirectory(_directory);
        _directory = null;
        _source = null;
    }

    private static bool IsMounted(string source, string modsDirectory)
    {
        return Directory.Exists(modsDirectory) && Directory.EnumerateDirectories(modsDirectory)
            .Where(path => !Path.GetFileName(path).StartsWith(".hat-", StringComparison.OrdinalIgnoreCase))
            .Any(path => string.Equals(GetDirectoryPath(path), source, PathComparison));
    }

    private static string GetDirectoryPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        var directory = new DirectoryInfo(path);
        return Path.TrimEndingDirectorySeparator(directory.ResolveLinkTarget(true)?.FullName ?? directory.FullName);
    }

    private static bool IsWithin(string path, string directory)
    {
        return string.Equals(path, directory, PathComparison) ||
               path.StartsWith(directory + Path.DirectorySeparatorChar, PathComparison);
    }

    private static void RemoveTemporaryDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        var directory = new DirectoryInfo(path);
        var marker = Path.Combine(path, MarkerName);
        if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint) ||
            !File.Exists(marker) || File.ReadAllText(marker) != MarkerContents)
        {
            throw new IOException($"Refusing to replace a directory not managed by FEZEditor: {path}");
        }

        Directory.Delete(path, recursive: true);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            if (Path.GetFileName(file) != MarkerName)
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
            }
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }
}
