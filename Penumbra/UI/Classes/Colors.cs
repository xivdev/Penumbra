using ImGuiNET;
using OtterGui.Custom;

namespace Penumbra.UI.Classes;

public enum ColorId : short
{
    EnabledMod,
    DisabledMod,
    UndefinedMod,
    InheritedMod,
    InheritedDisabledMod,
    NewMod,
    NewModTint,
    ConflictingMod,
    HandledConflictMod,
    FolderExpanded,
    FolderCollapsed,
    FolderLine,
    ItemId,
    IncreasedMetaValue,
    DecreasedMetaValue,
    SelectedCollection,
    RedundantAssignment,
    NoModsAssignment,
    NoAssignment,
    SelectorPriority,
    InGameHighlight,
    InGameHighlight2,
    ResTreeLocalPlayer,
    ResTreePlayer,
    ResTreeNetworked,
    ResTreeNonNetworked,
    PredefinedTagAdd,
    PredefinedTagRemove,
    TemporaryModSettingsTint,
    NoTint,
}

public static class Colors
{
    // These are written as 0xAABBGGRR.
    public const uint PressEnterWarningBg = 0xFF202080;
    public const uint RegexWarningBorder  = 0xFF0000B0;
    public const uint MetaInfoText        = 0xAAFFFFFF;
    public const uint RedTableBgTint      = 0x40000080;
    public const uint DiscordColor        = CustomGui.DiscordColor;
    public const uint FilterActive        = 0x807070FF;
    public const uint TutorialMarker      = 0xFF20FFFF;
    public const uint TutorialBorder      = 0xD00000FF;
    public const uint ReniColorButton     = CustomGui.ReniColorButton;
    public const uint ReniColorHovered    = CustomGui.ReniColorHovered;
    public const uint ReniColorActive     = CustomGui.ReniColorActive;

    public static uint Tinted(this ColorId color, ColorId tint)
    {
        var tintValue = ImGui.ColorConvertU32ToFloat4(tint.Value());
        var value     = ImGui.ColorConvertU32ToFloat4(color.Value());
        return ImGui.ColorConvertFloat4ToU32(TintColor(value, tintValue));
    }

    public static unsafe uint Tinted(this ImGuiCol color, ColorId tint)
    {
        var     tintValue = ImGui.ColorConvertU32ToFloat4(tint.Value());
        ref var value     = ref *ImGui.GetStyleColorVec4(color);
        return ImGui.ColorConvertFloat4ToU32(TintColor(value, tintValue));
    }

    private static unsafe Vector4 TintColor(in Vector4 color, in Vector4 tint)
    {
        var negAlpha = 1 - tint.W;
        var newAlpha = negAlpha * color.W + tint.W;
        var newR     = (negAlpha * color.W * color.X + tint.W * tint.X) / newAlpha;
        var newG     = (negAlpha * color.W * color.Y + tint.W * tint.Y) / newAlpha;
        var newB     = (negAlpha * color.W * color.Z + tint.W * tint.Z) / newAlpha;
        return new Vector4(newR, newG, newB, newAlpha);
    }

    public static (uint DefaultColor, string Name, string Description) Data(this ColorId color)
        => color switch
        {
            // @formatter:off
            ColorId.EnabledMod                    => ( 0xFFFFFFFF, "Enabled Mod",                           "A mod that is enabled by the currently selected collection." ),
            ColorId.DisabledMod                   => ( 0xFF686880, "Disabled Mod",                          "A mod that is disabled by the currently selected collection." ),
            ColorId.UndefinedMod                  => ( 0xFF808080, "Mod With No Settings",                  "A mod that is not configured in the currently selected collection or any of the collections it inherits from, and thus implicitly disabled." ),
            ColorId.InheritedMod                  => ( 0xFFD0FFFF, "Mod Enabled By Inheritance",            "A mod that is not configured in the currently selected collection, but enabled in a collection it inherits from." ),
            ColorId.InheritedDisabledMod          => ( 0xFF688080, "Mod Disabled By Inheritance",           "A mod that is not configured in the currently selected collection, but disabled in a collection it inherits from."),
            ColorId.NewMod                        => ( 0xFF66DD66, "New Mod",                               "A mod that was newly imported or created during this session and has not been enabled yet." ),
            ColorId.ConflictingMod                => ( 0xFFAAAAFF, "Mod With Unresolved Conflicts",         "An enabled mod that has conflicts with another enabled mod on the same priority level." ),
            ColorId.HandledConflictMod            => ( 0xFFD0FFD0, "Mod With Resolved Conflicts",           "An enabled mod that has conflicts with another enabled mod on a different priority level." ),
            ColorId.FolderExpanded                => ( 0xFFFFF0C0, "Expanded Mod Folder",                   "A mod folder that is currently expanded." ),
            ColorId.FolderCollapsed               => ( 0xFFFFF0C0, "Collapsed Mod Folder",                  "A mod folder that is currently collapsed." ),
            ColorId.FolderLine                    => ( 0xFFFFF0C0, "Expanded Mod Folder Line",              "The line signifying which descendants belong to an expanded mod folder." ),
            ColorId.ItemId                        => ( 0xFF808080, "Item Id",                               "The numeric model id of the given item to the right of changed items." ),
            ColorId.IncreasedMetaValue            => ( 0x80008000, "Increased Meta Manipulation Value",     "An increased meta manipulation value for floats or an enabled toggle where the default is disabled."),
            ColorId.DecreasedMetaValue            => ( 0x80000080, "Decreased Meta Manipulation Value",     "A decreased meta manipulation value for floats or a disabled toggle where the default is enabled."),
            ColorId.SelectedCollection            => ( 0x6069C056, "Currently Selected Collection",         "The collection that is currently selected and being edited."),
            ColorId.RedundantAssignment           => ( 0x6050D0D0, "Redundant Collection Assignment",       "A collection assignment that currently has no effect as it is redundant with more general assignments."),
            ColorId.NoModsAssignment              => ( 0x50000080, "'Use No Mods' Collection Assignment",   "A collection assignment set to not use any mods at all."),
            ColorId.NoAssignment                  => ( 0x00000000, "Unassigned Collection Assignment",      "A collection assignment that is not configured to any collection and thus just has no specific treatment."),
            ColorId.SelectorPriority              => ( 0xFF808080, "Mod Selector Priority",                 "The priority displayed for non-zero priority mods in the mod selector."),
            ColorId.InGameHighlight               => ( 0xFFEBCF89, "In-Game Highlight (Primary)",           "An in-game element that has been highlighted for ease of editing."),
            ColorId.InGameHighlight2              => ( 0xFF446CC0, "In-Game Highlight (Secondary)",         "Another in-game element that has been highlighted for ease of editing."),
            ColorId.ResTreeLocalPlayer            => ( 0xFFFFE0A0, "On-Screen: You",                        "You and what you own (mount, minion, accessory, pets and so on), in the On-Screen tab." ),
            ColorId.ResTreePlayer                 => ( 0xFFC0FFC0, "On-Screen: Other Players",              "Other players and what they own, in the On-Screen tab." ),
            ColorId.ResTreeNetworked              => ( 0xFFFFFFFF, "On-Screen: Non-Players (Networked)",    "Non-player entities handled by the game server, in the On-Screen tab." ),
            ColorId.ResTreeNonNetworked           => ( 0xFFC0C0FF, "On-Screen: Non-Players (Local)",        "Non-player entities handled locally, in the On-Screen tab." ),
            ColorId.PredefinedTagAdd              => ( 0xFF44AA44, "Predefined Tags: Add Tag",              "A predefined tag that is not present on the current mod and can be added." ),
            ColorId.PredefinedTagRemove           => ( 0xFF2222AA, "Predefined Tags: Remove Tag",           "A predefined tag that is already present on the current mod and can be removed." ),
            ColorId.TemporaryModSettingsTint      => ( 0x30FF0000, "Mod with Temporary Settings",           "A mod that has temporary settings. This color is used as a tint for the regular state colors." ),
            ColorId.NewModTint                    => ( 0x8000FF00, "New Mod Tint",                          "A mod that was newly imported or created during this session and has not been enabled yet. This color is used as a tint for the regular state colors."),
            ColorId.NoTint                        => ( 0x00000000, "No Tint",                               "The default tint for all mods."),
            _                                     => throw new ArgumentOutOfRangeException( nameof( color ), color, null ),
            // @formatter:on
        };

    private static IReadOnlyDictionary<ColorId, uint> _colors = new Dictionary<ColorId, uint>();

    /// <summary> Obtain the configured value for a color. </summary>
    public static uint Value(this ColorId color)
        => _colors.TryGetValue(color, out var value) ? value : color.Data().DefaultColor;

    /// <summary> Set the configurable colors dictionary to a value. </summary>
    public static void SetColors(Configuration config)
        => _colors = config.Colors;
}
