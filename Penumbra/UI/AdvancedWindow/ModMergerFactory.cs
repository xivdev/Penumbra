using Luna;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public sealed class ModMergerFactory(
    ModManager manager,
    ModGroupEditor groupEditor,
    DuplicateManager duplicates,
    CommunicatorService communicator,
    ModCreator creator,
    Configuration config) : IService
{
    public ModMerger CreateMerger(ModEditor editor)
        => new(manager, groupEditor, duplicates, communicator, creator, config, editor);

    public ModMergeTab CreateTab(ModMerger merger)
        => new(merger, new ModComboWithoutCurrent(manager, merger));

    public ModMergeTab CreateTab(ModEditor editor)
        => CreateTab(CreateMerger(editor));
}
