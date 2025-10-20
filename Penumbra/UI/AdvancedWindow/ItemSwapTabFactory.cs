using Luna;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Data;
using Penumbra.Meta;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI.ModsTab;

namespace Penumbra.UI.AdvancedWindow;

public class ItemSwapTabFactory(CommunicatorService communicator, ItemData itemService, CollectionManager collectionManager,
    ModManager modManager, ModFileSystemSelector selector, ObjectIdentification identifier, MetaFileManager metaFileManager,
    Configuration config) : IUiService
{
    public ItemSwapTab Create()
        => new(communicator, itemService, collectionManager, modManager, selector, identifier, metaFileManager, config);
}
