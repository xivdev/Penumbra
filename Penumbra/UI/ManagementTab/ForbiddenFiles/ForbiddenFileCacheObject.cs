using ImSharp;

namespace Penumbra.UI.ManagementTab;

public sealed class ForbiddenFileCacheObject(ForbiddenFileRedirection redirection)
    : RedirectionCacheObject<ForbiddenFileRedirection>(redirection)
{
    public static readonly StringPair Missing   = new("Missing", new StringU8("Missing"u8));
    public static readonly StringPair Broken    = new("Broken", new StringU8("Broken"u8));
    public static readonly StringPair Equal     = new("Equal", new StringU8("Equal"u8));
    public static readonly StringPair Different = new("Different", new StringU8("Different"u8));
    public static readonly StringPair Swap      = new("Swap", new StringU8("Swap"u8));

    public readonly StringPair State = redirection.FileSwap
        ? Swap
        : redirection.Missing
            ? Missing
            : redirection.Broken
                ? Broken
                : redirection.ConceptuallyEqual
                    ? Equal
                    : Different;
}
