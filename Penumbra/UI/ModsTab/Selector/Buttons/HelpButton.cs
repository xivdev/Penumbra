using ImSharp;
using Luna;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab.Selector;

/// <summary> The button to open the help popup. </summary>
public sealed class HelpButton(ModFileSystemDrawer drawer) : BaseIconButton<AwesomeIcon>
{
    /// <inheritdoc/>
    public override AwesomeIcon Icon
        => LunaStyle.InfoIcon;

    /// <inheritdoc/>
    public override bool HasTooltip
        => true;

    /// <inheritdoc/>
    public override void DrawTooltip()
        => Im.Text("Open extended help."u8);

    /// <inheritdoc/>
    public override void OnClick()
        => Im.Popup.Open("ExHelp"u8);

    /// <inheritdoc/>
    protected override void PostDraw()
    {
        drawer.Tutorial.OpenTutorial(BasicTutorialSteps.AdvancedHelp);
        ImEx.HelpPopup("ExtendedHelp"u8, new Vector2(1000 * Im.Style.GlobalScale, 38.5f * Im.Style.TextHeightWithSpacing), PopupContent);
    }

    private void PopupContent()
    {
        Im.Line.New();
        Im.Text("Mod Management"u8);
        Im.BulletText("You can create empty mods or import mods with the buttons in this row."u8);
        using var indent = Im.Indent();
        Im.BulletText("Supported formats for import are: .ttmp, .ttmp2, .pmp, .pcp."u8);
        Im.BulletText(
            "You can also support .zip, .7z or .rar archives, but only if they already contain Penumbra-styled mods with appropriate metadata."u8);
        indent.Unindent();
        Im.BulletText("You can also create empty mod folders and delete mods."u8);
        Im.BulletText(
            "For further editing of mods, select them and use the Edit Mod tab in the panel or the Advanced Editing popup."u8);
        Im.Line.New();
        Im.Text("Mod Selector"u8);
        Im.BulletText("Select a mod to obtain more information or change settings."u8);
        Im.BulletText("Names are colored according to your config and their current state in the collection:"u8);
        indent.Indent();
        Im.BulletText("enabled in the current collection."u8,                   ColorId.EnabledMod.Value());
        Im.BulletText("disabled in the current collection."u8,                  ColorId.DisabledMod.Value());
        Im.BulletText("enabled due to inheritance from another collection."u8,  ColorId.InheritedMod.Value());
        Im.BulletText("disabled due to inheritance from another collection."u8, ColorId.InheritedDisabledMod.Value());
        Im.BulletText("unconfigured in all inherited collections."u8,           ColorId.UndefinedMod.Value());
        Im.BulletText("enabled and conflicting with another enabled Mod, but on different priorities (i.e. the conflict is solved)."u8,
            ColorId.HandledConflictMod.Value());
        Im.BulletText("enabled and conflicting with another enabled Mod on the same priority."u8, ColorId.ConflictingMod.Value());
        Im.BulletText("expanded mod folder."u8,                                                   ColorId.FolderExpanded.Value());
        Im.BulletText("collapsed mod folder"u8,                                                   ColorId.FolderCollapsed.Value());
        indent.Unindent();
        Im.BulletText("Middle-click a mod to disable it if it is enabled or enable it if it is disabled."u8);
        indent.Indent();
        Im.BulletText(
            $"Holding {drawer.Config.DeleteModModifier.ForcedModifier(new DoubleModifier(ModifierHotkey.Control, ModifierHotkey.Shift))} while middle-clicking lets it inherit, discarding settings.");
        indent.Unindent();
        Im.BulletText("Right-click a mod to enter its sort order, which is its name by default, possibly with a duplicate number."u8);
        indent.Indent();
        Im.BulletText("A sort order differing from the mods name will not be displayed, it will just be used for ordering."u8);
        Im.BulletText(
            "If the sort order string contains Forward-Slashes ('/'), the preceding substring will be turned into folders automatically."u8);
        indent.Unindent();
        Im.BulletText(
            "You can drag and drop mods and subfolders into existing folders. Dropping them onto mods is the same as dropping them onto the parent of the mod."u8);
        indent.Indent();
        Im.BulletText(
            "You can select multiple mods and folders by holding Control while clicking them, and then drag all of them at once."u8);
        Im.BulletText(
            "Selected mods inside an also selected folder will be ignored when dragging and move inside their folder instead of directly into the target."u8);
        indent.Unindent();
        Im.BulletText("Right-clicking a folder opens a context menu."u8);
        Im.BulletText("Right-clicking empty space allows you to expand or collapse all folders at once."u8);
        Im.BulletText("Use the Filter Mods... input at the top to filter the list for mods whose name or path contain the text."u8);
        indent.Indent();
        Im.BulletText("You can enter n:[string] to filter only for names, without path."u8);
        Im.BulletText("You can enter c:[string] to filter for Changed Items instead."u8);
        Im.BulletText("You can enter a:[string] to filter for Mod Authors instead."u8);
        indent.Unindent();
        Im.BulletText("Use the expandable menu beside the input to filter for mods fulfilling specific criteria."u8);
    }
}
