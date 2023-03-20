using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Penumbra.String.Classes;

namespace Penumbra.Mods;

public class FileRegistry : IEquatable<FileRegistry>
{
    public readonly List<(ISubMod, Utf8GamePath)> SubModUsage = new();
    public          FullPath                      File     { get; private init; }
    public          Utf8RelPath                   RelPath  { get; private init; }
    public          long                          FileSize { get; private init; }
    public          int                           CurrentUsage;

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
