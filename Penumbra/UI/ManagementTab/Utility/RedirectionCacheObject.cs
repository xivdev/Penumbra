using ImSharp;
using Penumbra.Mods;

namespace Penumbra.UI.ManagementTab;

public class RedirectionCacheObject<T> where T : BaseScannedRedirection
{
    private static readonly StringPair  None = new("<None>", new StringU8("<None>"));

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
            : (None, None);
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
