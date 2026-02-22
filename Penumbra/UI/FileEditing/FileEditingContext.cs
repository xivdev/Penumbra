using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Mods;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.UI.FileEditing;

public sealed class FileEditingContext(ActiveCollections activeCollections, Mod? mod)
{
    public ModCollection? Collection
        => activeCollections.Current;

    public Mod? Mod
        => mod;

    /// <summary>
    /// Find the best matching associated file for a given path.
    /// </summary>
    /// <remarks>
    /// Tries to resolve from the current collection first and chooses the currently resolved file if any exists.
    /// If none exists, goes through all options in the currently selected mod (if any) in order of priority and resolves in them. 
    /// If no redirection is found in either of those options, returns the original path.
    /// </remarks>
    public FullPath FindBestMatch(Utf8GamePath path)
    {
        if (Collection is { } collection)
        {
            var currentFile = collection.ResolvePath(path);
            if (currentFile is not null)
                return currentFile.Value;
        }

        if (mod is not null)
        {
            foreach (var option in mod.Groups.OrderByDescending(g => g.Priority))
            {
                if (option.FindBestMatch(path) is { } fullPath)
                    return fullPath;
            }

            if (mod.Default.Files.TryGetValue(path, out var value) || mod.Default.FileSwaps.TryGetValue(path, out value))
                return value;
        }

        return new FullPath(path);
    }

    public IEnumerable<Utf8GamePath> FindPathsStartingWith(CiByteString prefix)
    {
        var ret = new HashSet<Utf8GamePath>();
        if (Collection is { } collection)
            foreach (var path in collection.ResolvedFiles.Keys)
            {
                if (path.Path.StartsWith(prefix))
                    ret.Add(path);
            }

        if (mod is not null)
            foreach (var option in mod.AllDataContainers)
                foreach (var path in option.Files.Keys.Where(path => path.Path.StartsWith(prefix)))
                    ret.Add(path);

        return ret;
    }
}
