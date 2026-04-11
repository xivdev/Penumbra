using ImSharp;
using Penumbra.Mods;

namespace Penumbra.UI.ManagementTab;

internal static class CacheObject
{
    public static readonly StringPair None = new("<None>", new StringU8("<None>"u8));
}

public class RedirectionCacheObject<T> where T : BaseScannedRedirection
{
    public readonly T          ScannedObject;
    public readonly StringPair GamePath;
    public readonly StringPair Target;
    public readonly StringPair Mod;
    public readonly StringPair Container;

    protected RedirectionCacheObject(T redirection)
    {
        ScannedObject = redirection;
        GamePath      = new StringPair(redirection.GamePath.Path.Span);
        Target        = GetTarget(redirection);
        (Container, Mod) = redirection.Container.TryGetTarget(out var container)
            ? (new StringPair(container.GetFullName()), new StringPair(container.Mod.Name))
            : (CacheObject.None, CacheObject.None);
    }

    private static StringPair GetTarget(T redirection)
    {
        if (redirection.FileSwap)
            return new StringPair(redirection.Redirection.FullName);

        if (!redirection.Container.TryGetTarget(out var container)
         || container.Mod is not Mod mod
         || !redirection.Redirection.ToRelPath(mod.ModPath, out var path))
            return new StringPair(redirection.Redirection.FullName);

        return new StringPair(path.Path.Span);
    }
}

public class FileCacheObject<T> where T : BaseScannedFile
{
    public readonly T          ScannedObject;
    public readonly StringPair File;
    public readonly StringPair Mod;

    protected FileCacheObject(T file)
    {
        ScannedObject = file;
        Mod           = file.Mod.TryGetTarget(out var m) ? new StringPair(m.Name) : CacheObject.None;
        File          = new StringPair(file.RelativePath);
    }
}
