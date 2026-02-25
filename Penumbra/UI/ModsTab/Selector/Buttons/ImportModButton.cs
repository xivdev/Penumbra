using ImSharp;
using Luna;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab.Selector;

/// <summary> The button to import a mod. </summary>
public sealed class ImportModButton(ModFileSystemDrawer drawer) : BaseIconButton<AwesomeIcon>
{
    /// <inheritdoc/>
    public override AwesomeIcon Icon
        => LunaStyle.ImportIcon;

    /// <inheritdoc/>
    public override bool HasTooltip
        => true;

    /// <inheritdoc/>
    public override void DrawTooltip()
        => Im.Text("Import one or multiple mods from Tex Tools Mod Pack Files or Penumbra Mod Pack Files."u8);

    /// <inheritdoc/>
    public override void OnClick()
    {
        var modPath = drawer.Config.DefaultModImportPath.Length > 0
            ? drawer.Config.DefaultModImportPath
            : drawer.Config.ModDirectory.Length > 0
                ? drawer.Config.ModDirectory
                : null;

        drawer.FileService.OpenFilePicker("Import Mod Pack",
            "Mod Packs{.ttmp,.ttmp2,.pmp,.pcp},TexTools Mod Packs{.ttmp,.ttmp2},Penumbra Mod Packs{.pmp,.pcp},Archives{.zip,.7z,.rar},Penumbra Character Packs{.pcp}",
            (s, f) =>
            {
                if (!s)
                    return;

                drawer.ModImport.AddUnpack(f);
            }, 0, modPath, drawer.Config.AlwaysOpenDefaultImport);
    }

    /// <inheritdoc/>
    protected override void PostDraw()
        => drawer.Tutorial.OpenTutorial(BasicTutorialSteps.ModImport);
}
