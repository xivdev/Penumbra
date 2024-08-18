using Dalamud.Plugin.Services;
using OtterGui.Services;
using Penumbra.GameData.Files;
using Penumbra.GameData.Interop;
using Penumbra.Interop.Hooks.Objects;
using Penumbra.Interop.ResourceTree;
using Penumbra.Services;

namespace Penumbra.UI.AdvancedWindow.Materials;

public sealed class MtrlTabFactory(
    IDataManager gameData,
    IFramework framework,
    ObjectManager objects,
    CharacterBaseDestructor characterBaseDestructor,
    StainService stainService,
    ResourceTreeFactory resourceTreeFactory,
    FileDialogService fileDialog,
    MaterialTemplatePickers materialTemplatePickers,
    Configuration config) : IUiService
{
    public MtrlTab Create(ModEditWindow edit, MtrlFile file, string filePath, bool writable)
        => new(gameData, framework, objects, characterBaseDestructor, stainService, resourceTreeFactory, fileDialog,
            materialTemplatePickers, config, edit, file, filePath, writable);
}
