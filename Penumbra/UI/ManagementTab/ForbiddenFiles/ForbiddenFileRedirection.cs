using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;

namespace Penumbra.UI.ManagementTab;

public sealed class ForbiddenFileRedirection(
    Utf8GamePath path,
    FullPath redirection,
    IModDataContainer container,
    bool swap,
    bool missing,
    bool conceptuallyEqual,
    bool broken = false)
    : BaseScannedRedirection(path, redirection, container, swap)
{
    public readonly bool Missing           = missing;
    public readonly bool ConceptuallyEqual = conceptuallyEqual;
    public readonly bool Broken            = broken;
}
