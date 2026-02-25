using Luna;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Data;
using Penumbra.Meta;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Services;

namespace Penumbra.UI.AdvancedWindow;

public class ItemSwapTabFactory(
    CommunicatorService communicator,
    ItemData itemService,
    CollectionManager collectionManager,
    ModManager modManager,
    ModSelection selection,
    ObjectIdentification identifier,
    MetaFileManager metaFileManager,
    Configuration config) : IUiService
{
    public ItemSwapTab Create()
        => new(communicator, itemService, collectionManager, modManager, selection, identifier, metaFileManager, config);
}
