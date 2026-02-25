using Luna;
using Penumbra.Meta;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Services;

namespace Penumbra.Mods.Editor;

public class ModEditorFactory(
    ModNormalizer modNormalizer,
    ModGroupEditor groupEditor,
    MetaFileManager metaFileManager,
    ModManager modManager,
    CommunicatorService communicator,
    DuplicateManager duplicates,
    FileCompactor compactor) : IService
{
    public ModEditor Create()
    {
        var metaEditor        = new ModMetaEditor(groupEditor, metaFileManager);
        var files             = new ModFileCollection();
        var fileEditor        = new ModFileEditor(files, modManager, communicator);
        var swapEditor        = new ModSwapEditor(modManager);
        var mdlMaterialEditor = new MdlMaterialEditor(files);

        return new(modNormalizer, metaEditor, files, fileEditor, duplicates, swapEditor, mdlMaterialEditor, compactor);
    }
}
