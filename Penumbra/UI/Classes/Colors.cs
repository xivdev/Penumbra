using ImSharp;
using Luna;

namespace Penumbra.UI.Classes;

public enum ColorId
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
    ChangedItemPreferenceStar,
    NoTint,
    OptionColor1,
    OptionColor2,
    OptionColor3,
    OptionColor4,
    OptionColor5,
    OptionColor6,
    OptionColor7,
    OptionColor8,
    OptionTreeLine,
    GroupLabelBackground,
    GroupLabelBorder,
    GroupLabelText,
    GroupLabelBackgroundExpanded,
    GroupLabelBorderExpanded,
    GroupLabelTextExpanded,
    GroupLabelBackgroundCollapsed,
    GroupLabelBorderCollapsed,
    GroupLabelTextCollapsed,
    OptionBorder,
}

public static class Colors
{
    // These are written as 0xAABBGGRR.
    public static readonly Vector4 PressEnterWarningBg = new(0.5f, 0.125f, 0.125f, 1);
    public static readonly Vector4 RegexWarningBorder  = new(0.7f, 0, 0, 1);
    public static readonly Vector4 MetaInfoText        = new(1, 1, 1, 2f / 3);
    public const           uint    RedTableBgTint      = 0x40000080;
    public const           uint    FilterActive        = 0x807070FF;
    public const           uint    TutorialMarker      = 0xFF20FFFF;
    public const           uint    TutorialBorder      = 0xD00000FF;

    private const string OptionColorTooltip =
        "A color used for the selectable text or label for a mod option. The mod creator can associate one of these 8 colors (or none for default text), but you can choose the actual color.";

    public static (uint DefaultColor, string Name, string Description) Data(this ColorId color)
        => color switch
        {
            // @formatter:off
            ColorId.EnabledMod                    => ( 0xFFFFFFFF, "Enabled Mod",                                     "A mod that is enabled by the currently selected collection." ),
            ColorId.DisabledMod                   => ( 0xFF686880, "Disabled Mod",                                    "A mod that is disabled by the currently selected collection." ),
            ColorId.UndefinedMod                  => ( 0xFF808080, "Mod With No Settings",                            "A mod that is not configured in the currently selected collection or any of the collections it inherits from, and thus implicitly disabled." ),
            ColorId.InheritedMod                  => ( 0xFFD0FFFF, "Mod Enabled By Inheritance",                      "A mod that is not configured in the currently selected collection, but enabled in a collection it inherits from." ),
            ColorId.InheritedDisabledMod          => ( 0xFF688080, "Mod Disabled By Inheritance",                     "A mod that is not configured in the currently selected collection, but disabled in a collection it inherits from."),
            ColorId.NewMod                        => ( 0xFF66DD66, "New Mod",                                         "A mod that was newly imported or created during this session and has not been enabled yet." ),
            ColorId.ConflictingMod                => ( 0xFFAAAAFF, "Mod With Unresolved Conflicts",                   "An enabled mod that has conflicts with another enabled mod on the same priority level." ),
            ColorId.HandledConflictMod            => ( 0xFFD0FFD0, "Mod With Resolved Conflicts",                     "An enabled mod that has conflicts with another enabled mod on a different priority level." ),
            ColorId.FolderExpanded                => ( 0xFFFFF0C0, "Expanded Mod Folder",                             "A mod folder that is currently expanded." ),
            ColorId.FolderCollapsed               => ( 0xFFFFF0C0, "Collapsed Mod Folder",                            "A mod folder that is currently collapsed." ),
            ColorId.FolderLine                    => ( 0xFFFFF0C0, "Expanded Mod Folder Line",                        "The line signifying which descendants belong to an expanded mod folder." ),
            ColorId.ItemId                        => ( 0xFF808080, "Item Id",                                         "The numeric model id of the given item to the right of changed items." ),
            ColorId.IncreasedMetaValue            => ( 0x80008000, "Increased Meta Manipulation Value",               "An increased meta manipulation value for floats or an enabled toggle where the default is disabled."),
            ColorId.DecreasedMetaValue            => ( 0x80000080, "Decreased Meta Manipulation Value",               "A decreased meta manipulation value for floats or a disabled toggle where the default is enabled."),
            ColorId.SelectedCollection            => ( 0x6069C056, "Currently Selected Collection",                   "The collection that is currently selected and being edited."),
            ColorId.RedundantAssignment           => ( 0x6050D0D0, "Redundant Collection Assignment",                 "A collection assignment that currently has no effect as it is redundant with more general assignments."),
            ColorId.NoModsAssignment              => ( 0x50000080, "'Use No Mods' Collection Assignment",             "A collection assignment set to not use any mods at all."),
            ColorId.NoAssignment                  => ( 0x00000000, "Unassigned Collection Assignment",                "A collection assignment that is not configured to any collection and thus just has no specific treatment."),
            ColorId.SelectorPriority              => ( 0xFF808080, "Mod Selector Priority",                           "The priority displayed for non-zero priority mods in the mod selector."),
            ColorId.InGameHighlight               => ( 0xFFEBCF89, "In-Game Highlight (Primary)",                     "An in-game element that has been highlighted for ease of editing."),
            ColorId.InGameHighlight2              => ( 0xFF446CC0, "In-Game Highlight (Secondary)",                   "Another in-game element that has been highlighted for ease of editing."),
            ColorId.ResTreeLocalPlayer            => ( 0xFFFFE0A0, "On-Screen: You",                                  "You and what you own (mount, minion, accessory, pets and so on), in the On-Screen tab." ),
            ColorId.ResTreePlayer                 => ( 0xFFC0FFC0, "On-Screen: Other Players",                        "Other players and what they own, in the On-Screen tab." ),
            ColorId.ResTreeNetworked              => ( 0xFFFFFFFF, "On-Screen: Non-Players (Networked)",              "Non-player entities handled by the game server, in the On-Screen tab." ),
            ColorId.ResTreeNonNetworked           => ( 0xFFC0C0FF, "On-Screen: Non-Players (Local)",                  "Non-player entities handled locally, in the On-Screen tab." ),
            ColorId.PredefinedTagAdd              => ( 0xFF44AA44, "Predefined Tags: Add Tag",                        "A predefined tag that is not present on the current mod and can be added." ),
            ColorId.PredefinedTagRemove           => ( 0xFF2222AA, "Predefined Tags: Remove Tag",                     "A predefined tag that is already present on the current mod and can be removed." ),
            ColorId.TemporaryModSettingsTint      => ( 0x30FF0000, "Mod with Temporary Settings",                     "A mod that has temporary settings. This color is used as a tint for the regular state colors." ),
            ColorId.NewModTint                    => ( 0x8000FF00, "New Mod Tint",                                    "A mod that was newly imported or created during this session and has not been enabled yet. This color is used as a tint for the regular state colors."),
            ColorId.NoTint                        => ( 0x00000000, "No Tint",                                         "The default tint for all mods."),
            ColorId.ChangedItemPreferenceStar     => ( 0x30FFFFFF, "Preferred Changed Item Star",                     "The color of the star button in the mod panel's changed items tab to prioritize specific items."),
            ColorId.OptionColor1                  => ( 0xFFF8CD8E, "Selectable Color for Mod Option #1",              OptionColorTooltip),
            ColorId.OptionColor2                  => ( 0xFFAAD898, "Selectable Color for Mod Option #2",              OptionColorTooltip),
            ColorId.OptionColor3                  => ( 0xFF8AD1E6, "Selectable Color for Mod Option #3",              OptionColorTooltip),
            ColorId.OptionColor4                  => ( 0xFF6B8CD9, "Selectable Color for Mod Option #4",              OptionColorTooltip),
            ColorId.OptionColor5                  => ( 0xFFA38FD9, "Selectable Color for Mod Option #5",              OptionColorTooltip),
            ColorId.OptionColor6                  => ( 0xFFDB9DB3, "Selectable Color for Mod Option #6",              OptionColorTooltip),
            ColorId.OptionColor7                  => ( 0xFF6A5CC7, "Selectable Color for Mod Option #7",              OptionColorTooltip),
            ColorId.OptionColor8                  => ( 0xFF6BB5A6, "Selectable Color for Mod Option #8",              OptionColorTooltip),
            ColorId.OptionTreeLine                => ( 0x80FFF0C0, "Option Group Dependency Tree Line",               "The color for the line connecting option groups and nodes in the mod settings panel."),
            ColorId.GroupLabelBackground          => ( 0x8A4A4A4A, "Option Group Label Background (Non-Interactive)", "The color for the background of option group labels when they are not collapsible."),
            ColorId.GroupLabelBorder              => ( 0x80FFF0C0, "Option Group Label Border (Non-Interactive)",     "The color for the border around option group labels when they are not collapsible."),
            ColorId.GroupLabelText                => ( 0xFFFFFFFF, "Option Group Label Text (Non-Interactive)",       "The color for the text in option group labels when they are not collapsible."),
            ColorId.GroupLabelBackgroundExpanded  => ( 0x4F969696, "Option Group Label Background (Expanded)",        "The color for the background of option group labels when they are collapsible and currently expanded."),
            ColorId.GroupLabelBorderExpanded      => ( 0x80FFF0C0, "Option Group Label Border (Expanded)",            "The color for the border around option group labels when they are collapsible and currently expanded."),
            ColorId.GroupLabelTextExpanded        => ( 0xFFFFFFFF, "Option Group Label Text (Expanded)",              "The color for the text in option group labels when they are collapsible and currently expanded."),
            ColorId.GroupLabelBackgroundCollapsed => ( 0x4F969696, "Option Group Label Background (Collapsed)",       "The color for the background of option group labels when they are currently collapsed."),
            ColorId.GroupLabelBorderCollapsed     => ( 0x80FFF0C0, "Option Group Label Border (Collapsed)",           "The color for the border around option group labels when they are currently collapsed."),
            ColorId.GroupLabelTextCollapsed       => ( 0xFFFFFFFF, "Option Group Label Text (Collapsed)",             "The color for the text in option group labels when they are currently collapsed."),
            ColorId.OptionBorder                  => ( 0x80FFF0C0, "Option Checkbox/Radio Button/Combo Border",       "The color of the border around option checkboxes, radio buttons or single select option group combos."),
            _                                     => throw new ArgumentOutOfRangeException( nameof( color ), color, null ),
            // @formatter:on
        };

    private static ColorCache<ColorId, ColorIdData> _colors = null!;

    extension(ColorId color)
    {
        public Rgba32 Value
            => _colors[color];

        public Vector4 Vector
            => _colors[color, true];
    }

    extension(ImGuiColor color)
    {
        public Rgba32 Value
            => _colors[color];

        public Vector4 Vector
            => _colors[color, true];
    }

    internal static void SetCache(ColorCache<ColorId, ColorIdData> cache)
        => _colors = cache;
}
