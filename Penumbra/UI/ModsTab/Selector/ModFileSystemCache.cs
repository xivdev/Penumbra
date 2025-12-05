using Luna;

namespace Penumbra.UI.ModsTab.Selector;

public sealed class ModFileSystemCache(ModFileSystemDrawer parent)
    : FileSystemCache<ModFileSystemCache.ModData>(parent), IService
{
    public sealed class ModData : BaseFileSystemNodeCache<ModData>;

    public override void Update()
    { }

    protected override ModData ConvertNode(in IFileSystemNode node)
        => new();
}
