using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;

namespace Penumbra.Mods.Editor;

public class FileRegistry : IEquatable<FileRegistry>
{
    public readonly List<(IModDataContainer, Utf8GamePath)> SubModUsage = [];
    public          FullPath                                File     { get; private init; }
    public          Utf8RelPath                             RelPath  { get; private init; }
    public          long                                    FileSize { get; private init; }
    public          int                                     CurrentUsage;
    public          bool                                    IsOnPlayer;

    public static bool FromFile(DirectoryInfo modPath, FileInfo file, [NotNullWhen(true)] out FileRegistry? registry)
    {
        var fullPath = new FullPath(file.FullName);
        if (!fullPath.ToRelPath(modPath, out var relPath))
        {
            registry = null;
            return false;
        }

        registry = new FileRegistry
        {
            File         = fullPath,
            RelPath      = relPath,
            FileSize     = file.Length,
            CurrentUsage = 0,
            IsOnPlayer   = false,
        };
        return true;
    }

    public bool Equals(FileRegistry? other)
    {
        if (other is null)
            return false;

        return ReferenceEquals(this, other) || File.Equals(other.File);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;

        if (ReferenceEquals(this, obj))
            return true;

        return obj.GetType() == GetType() && Equals((FileRegistry)obj);
    }

    public override int GetHashCode()
        => File.GetHashCode();
}
