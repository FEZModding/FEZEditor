namespace FezEditor.Tools;

public abstract record ModDirectoryResolution(DirectoryInfo Original)
{
    public DirectoryInfo AssetsDirectory => this switch
    {
        Selected s => s.Assets,
        Redirected r => r.Assets,
        Created r => r.Assets,
        _ => throw new InvalidOperationException()
    };

    public DirectoryInfo ModRootDirectory => this switch
    {
        Selected s => s.ModRoot,
        Redirected r => r.ModRoot,
        Created r => r.ModRoot,
        _ => throw new InvalidOperationException()
    };

    public sealed record Invalid(DirectoryInfo Original)
        : ModDirectoryResolution(Original);

    public sealed record Selected(DirectoryInfo Original, DirectoryInfo Assets, DirectoryInfo ModRoot)
        : ModDirectoryResolution(Original);

    public sealed record Redirected(DirectoryInfo Original, DirectoryInfo Assets, DirectoryInfo ModRoot)
        : ModDirectoryResolution(Original);

    public sealed record Created(DirectoryInfo Original, DirectoryInfo Assets, DirectoryInfo ModRoot)
        : ModDirectoryResolution(Original);

    public static ModDirectoryResolution Resolve(DirectoryInfo directory)
    {
        if (!directory.Exists)
        {
            return new Invalid(directory);
        }

        const string assetsDirectoryName = "Assets";

        var parent = directory.Parent;
        if (directory.Name.Equals(assetsDirectoryName, StringComparison.OrdinalIgnoreCase) &&
            parent != null &&
            ContainsMetadata(parent))
        {
            return new Selected(directory, directory, parent);
        }

        if (ContainsMetadata(directory))
        {
            var assetsDirectory = FindDirectory(directory, assetsDirectoryName);
            if (assetsDirectory == null)
            {
                assetsDirectory = directory.CreateSubdirectory(assetsDirectoryName);
                return new Created(directory, assetsDirectory, directory);
            }

            return new Redirected(directory, assetsDirectory, directory);
        }

        return new Invalid(directory);
    }

    private static bool ContainsMetadata(DirectoryInfo directory)
    {
        return directory.EnumerateFiles()
            .Any(f => f.Name.Equals("Metadata.xml", StringComparison.OrdinalIgnoreCase));
    }

    private static DirectoryInfo? FindDirectory(DirectoryInfo directory, string name)
    {
        return directory.EnumerateDirectories()
            .FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}