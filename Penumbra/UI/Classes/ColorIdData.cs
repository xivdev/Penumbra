using ImSharp;
using Luna;
using static Penumbra.UI.Classes.ColorId;

namespace Penumbra.UI.Classes;

public readonly struct ColorIdData : IColorData<ColorId>
{
    private static readonly ColorData<ColorId>[] ColorData = CreateData();

    public static ColorData<ColorId> Data(in ColorId id)
    {
        if ((int)id < 0 || (int)id >= ColorData.Length)
            return ColorData<ColorId>.Invalid;

        return ColorData[(int)id];
    }

    public static StringU8 Parent { get; } = new("Penumbra"u8);

    private static readonly StringU8 OptionColorTooltip =
        new("A color used for the selectable text or label for a mod option. "u8
          + "The mod creator can associate one of these 8 colors (or none for default text), but you can choose the actual color."u8);

    private static ColorData<ColorId>[] CreateData()
    {
        var modSelector  = "Mod Selector"u8;
        var metadata     = "Metadata"u8;
        var collections  = "Collections"u8;
        var resourceTree = "On-Screen"u8;
        var modSettings  = "Mod Settings"u8;

        var ret = new ColorData<ColorId>[ColorId.Values.Count];
        // Mod Selector
        ret[(int)EnabledMod] = new ColorData<ColorId>(ImGuiColor.Text, "Enabled Mod"u8,
            "A mod that is enabled by the currently selected collection."u8, modSelector);
        ret[(int)DisabledMod] = new ColorData<ColorId>(0xFF686880, "Disabled Mod"u8,
            "A mod that is disabled by the currently selected collection."u8, modSelector);
        ret[(int)UndefinedMod] = new ColorData<ColorId>(ImGuiColor.TextDisabled, "Mod With No Settings"u8,
            "A mod that is not configured in the currently selected collection or any of the collections it inherits from, and thus implicitly disabled."u8,
            modSelector);
        ret[(int)InheritedMod] = new ColorData<ColorId>(0xFFD0FFFF, "Mod Enabled By Inheritance"u8,
            "A mod that is not configured in the currently selected collection, but enabled in a collection it inherits from."u8, modSelector);
        ret[(int)InheritedDisabledMod] = new ColorData<ColorId>(0xFF688080, "Mod Disabled By Inheritance"u8,
            "A mod that is not configured in the currently selected collection, but disabled in a collection it inherits from."u8, modSelector);
        ret[(int)NewMod] = new ColorData<ColorId>(DalamudColor.SuccessForeground, "New Mod"u8,
            "A mod that was newly imported or created during this session and has not been enabled yet."u8, modSelector);
        ret[(int)ConflictingMod] = new ColorData<ColorId>(DalamudColor.WarningBackground, "Mod With Unresolved Conflicts"u8,
            "An enabled mod that has conflicts with another enabled mod on the same priority level."u8, modSelector);
        ret[(int)NewModTint] = new ColorData<ColorId>(DalamudColor.SuccessForeground, "New Mod Tint"u8,
            "A mod that was newly imported or created during this session and has not been enabled yet. This color is used as a tint for the regular state colors."u8,
            modSelector);
        ret[(int)HandledConflictMod] = new ColorData<ColorId>(0xFFD0FFD0, "Mod With Resolved Conflicts"u8,
            "An enabled mod that has conflicts with another enabled mod on a different priority level."u8, modSelector);
        ret[(int)FolderExpanded] =
            new ColorData<ColorId>(FolderLine, "Expanded Mod Folder"u8, "A mod folder that is currently expanded."u8, modSelector);
        ret[(int)FolderCollapsed] = new ColorData<ColorId>(FolderLine, "Collapsed Mod Folder"u8,
            "A mod folder that is currently collapsed."u8, modSelector);
        ret[(int)FolderLine] = new ColorData<ColorId>(0xFFFFF0C0, "Expanded Mod Folder Line"u8,
            "The line signifying which descendants belong to an expanded mod folder."u8, modSelector);
        ret[(int)SelectorPriority] = new ColorData<ColorId>(ImGuiColor.TextDisabled, "Mod Selector Priority"u8,
            "The priority displayed for non-zero priority mods in the mod selector."u8, modSelector);
        ret[(int)TemporaryModSettingsTint] = new ColorData<ColorId>(0x30FF0000, "Mod with Temporary Settings"u8,
            "A mod that has temporary settings. This color is used as a tint for the regular state colors."u8, modSelector);
        ret[(int)NoTint] = new ColorData<ColorId>(Rgba32.Transparent, "No Tint"u8,
            "The default tint for all mods."u8, modSelector);

        // Meta stuff
        ret[(int)ItemId] = new ColorData<ColorId>(ImGuiColor.TextDisabled, "Item Id"u8,
            "The numeric model id of the given item to the right of changed items."u8, metadata);
        ret[(int)IncreasedMetaValue] = new ColorData<ColorId>(DalamudColor.SuccessBackground, "Increased Meta Manipulation Value"u8,
            "An increased meta manipulation value for floats or an enabled toggle where the default is disabled."u8, metadata);
        ret[(int)DecreasedMetaValue] = new ColorData<ColorId>(DalamudColor.ErrorBackground, "Decreased Meta Manipulation Value"u8,
            "A decreased meta manipulation value for floats or a disabled toggle where the default is enabled."u8, metadata);
        ret[(int)PredefinedTagAdd] = new ColorData<ColorId>(DalamudColor.SuccessBackground, "Predefined Tags: Add Tag"u8,
            "A predefined tag that is not present on the current mod and can be added."u8, metadata);
        ret[(int)PredefinedTagRemove] = new ColorData<ColorId>(DalamudColor.ErrorBackground, "Predefined Tags: Remove Tag"u8,
            "A predefined tag that is already present on the current mod and can be removed."u8, metadata);
        ret[(int)ChangedItemPreferenceStar] = new ColorData<ColorId>(0x30FFFFFF, "Preferred Changed Item Star"u8,
            "The color of the star button in the mod panel's changed items tab to prioritize specific items."u8, metadata);
        ret[(int)InGameHighlight] = new ColorData<ColorId>(0xFFEBCF89, "In-Game Highlight (Primary)"u8,
            "An in-game element that has been highlighted for ease of editing."u8, metadata);
        ret[(int)InGameHighlight2] = new ColorData<ColorId>(0xFF446CC0, "In-Game Highlight (Secondary)"u8,
            "Another in-game element that has been highlighted for ease of editing."u8, metadata);
        ret[(int)ModSpecificPreset] = new ColorData<ColorId>(DalamudColor.HealerGreen, "Mod-Specific Setting Preset"u8,
            "The color of a setting preset specific to this mod as opposed to a generic setting preset in the preset combo."u8, metadata);

        // Collections
        ret[(int)SelectedCollection] = new ColorData<ColorId>(0x6069C056, "Currently Selected Collection"u8,
            "The collection that is currently selected and being edited."u8, collections);
        ret[(int)RedundantAssignment] = new ColorData<ColorId>(DalamudColor.AttentionBackground, "Redundant Collection Assignment"u8,
            "A collection assignment that currently has no effect as it is redundant with more general assignments."u8, collections);
        ret[(int)NoModsAssignment] = new ColorData<ColorId>(0x50000080, "'Use No Mods' Collection Assignment"u8,
            "A collection assignment set to not use any mods at all."u8, collections);
        ret[(int)NoAssignment] = new ColorData<ColorId>(Rgba32.Transparent, "Unassigned Collection Assignment"u8,
            "A collection assignment that is not configured to any collection and thus just has no specific treatment."u8, collections);

        // Resource Tree
        ret[(int)ResTreeLocalPlayer] = new ColorData<ColorId>(0xFFFFE0A0, "On-Screen: You"u8,
            "You and what you own (mount, minion, accessory, pets and so on), in the On-Screen tab."u8, resourceTree);
        ret[(int)ResTreePlayer] = new ColorData<ColorId>(0xFFC0FFC0, "On-Screen: Other Players"u8,
            "Other players and what they own, in the On-Screen tab."u8, resourceTree);
        ret[(int)ResTreeNetworked] = new ColorData<ColorId>(ImGuiColor.Text, "On-Screen: Non-Players (Networked)"u8,
            "Non-player entities handled by the game server, in the On-Screen tab."u8, resourceTree);
        ret[(int)ResTreeNonNetworked] = new ColorData<ColorId>(0xFFC0C0FF, "On-Screen: Non-Players (Local)"u8,
            "Non-player entities handled locally, in the On-Screen tab."u8, resourceTree);

        // Mod Settings
        ret[(int)OptionColor1] = new ColorData<ColorId>(0xFFF8CD8E, "Selectable Color for Mod Option #1"u8, OptionColorTooltip, modSettings);
        ret[(int)OptionColor2] = new ColorData<ColorId>(0xFFAAD898, "Selectable Color for Mod Option #2"u8, OptionColorTooltip, modSettings);
        ret[(int)OptionColor3] = new ColorData<ColorId>(0xFF8AD1E6, "Selectable Color for Mod Option #3"u8, OptionColorTooltip, modSettings);
        ret[(int)OptionColor4] = new ColorData<ColorId>(0xFF6B8CD9, "Selectable Color for Mod Option #4"u8, OptionColorTooltip, modSettings);
        ret[(int)OptionColor5] = new ColorData<ColorId>(0xFFA38FD9, "Selectable Color for Mod Option #5"u8, OptionColorTooltip, modSettings);
        ret[(int)OptionColor6] = new ColorData<ColorId>(0xFFDB9DB3, "Selectable Color for Mod Option #6"u8, OptionColorTooltip, modSettings);
        ret[(int)OptionColor7] = new ColorData<ColorId>(0xFF6A5CC7, "Selectable Color for Mod Option #7"u8, OptionColorTooltip, modSettings);
        ret[(int)OptionColor8] = new ColorData<ColorId>(0xFF6BB5A6, "Selectable Color for Mod Option #8"u8, OptionColorTooltip, modSettings);
        ret[(int)OptionTreeLine] = new ColorData<ColorId>(ImGuiColor.Separator, "Option Group Dependency Tree Line"u8,
            "The color for the line connecting option groups and nodes in the mod settings panel."u8, modSettings);
        ret[(int)GroupLabelBackground] = new ColorData<ColorId>(ImGuiColor.TitleBackground, "Option Group Label Background (Non-Interactive)"u8,
            "The color for the background of option group labels when they are not collapsible."u8, modSettings);
        ret[(int)GroupLabelBorder] = new ColorData<ColorId>(OptionTreeLine, "Option Group Label Border (Non-Interactive)"u8,
            "The color for the border around option group labels when they are not collapsible."u8, modSettings);
        ret[(int)GroupLabelText] = new ColorData<ColorId>(ImGuiColor.Text, "Option Group Label Text (Non-Interactive)"u8,
            "The color for the text in option group labels when they are not collapsible."u8, modSettings);
        ret[(int)GroupLabelBackgroundExpanded] = new ColorData<ColorId>(ImGuiColor.Header, "Option Group Label Background (Expanded)"u8,
            "The color for the background of option group labels when they are collapsible and currently expanded."u8, modSettings);
        ret[(int)GroupLabelBorderExpanded] = new ColorData<ColorId>(OptionTreeLine, "Option Group Label Border (Expanded)"u8,
            "The color for the border around option group labels when they are collapsible and currently expanded."u8, modSettings);
        ret[(int)GroupLabelTextExpanded] = new ColorData<ColorId>(ImGuiColor.Text, "Option Group Label Text (Expanded)"u8,
            "The color for the text in option group labels when they are collapsible and currently expanded."u8, modSettings);
        ret[(int)GroupLabelBackgroundCollapsed] = new ColorData<ColorId>(ImGuiColor.Header, "Option Group Label Background (Collapsed)"u8,
            "The color for the background of option group labels when they are currently collapsed."u8, modSettings);
        ret[(int)GroupLabelBorderCollapsed] = new ColorData<ColorId>(OptionTreeLine, "Option Group Label Border (Collapsed)"u8,
            "The color for the border around option group labels when they are currently collapsed."u8, modSettings);
        ret[(int)GroupLabelTextCollapsed] = new ColorData<ColorId>(ImGuiColor.Text, "Option Group Label Text (Collapsed)"u8,
            "The color for the text in option group labels when they are currently collapsed."u8, modSettings);
        ret[(int)OptionBorder] = new ColorData<ColorId>(OptionTreeLine, "Option Checkbox/Radio Button/Combo Border"u8,
            "The color of the border around option checkboxes, radio buttons or single select option group combos."u8, modSettings);
        ret[(int)HiddenOptionIndicator] = new ColorData<ColorId>(0x00FFFFFF, "Hidden Option Indicator"u8,
            "The color of an indicator line when a group or option has more options or group children than are displayed."u8, modSettings);

        foreach (var data in ret)
        {
            if (data.Default.Value is 0)
                throw new SystemException("A color ID has no data assigned.");
        }

        return ret;
    }

    /// <summary> The old hardcoded default values used for migration. </summary>
    internal static Rgba32 OldDefault(ColorId id)
        => id switch
        {
            EnabledMod                    => 0xFFFFFFFF,
            DisabledMod                   => 0xFF686880,
            UndefinedMod                  => 0xFF808080,
            InheritedMod                  => 0xFFD0FFFF,
            InheritedDisabledMod          => 0xFF688080,
            NewMod                        => 0xFF66DD66,
            ConflictingMod                => 0xFFAAAAFF,
            HandledConflictMod            => 0xFFD0FFD0,
            FolderExpanded                => 0xFFFFF0C0,
            FolderCollapsed               => 0xFFFFF0C0,
            FolderLine                    => 0xFFFFF0C0,
            ItemId                        => 0xFF808080,
            IncreasedMetaValue            => 0x80008000,
            DecreasedMetaValue            => 0x80000080,
            SelectedCollection            => 0x6069C056,
            RedundantAssignment           => 0x6050D0D0,
            NoModsAssignment              => 0x50000080,
            NoAssignment                  => 0x00000000,
            SelectorPriority              => 0xFF808080,
            InGameHighlight               => 0xFFEBCF89,
            InGameHighlight2              => 0xFF446CC0,
            ResTreeLocalPlayer            => 0xFFFFE0A0,
            ResTreePlayer                 => 0xFFC0FFC0,
            ResTreeNetworked              => 0xFFFFFFFF,
            ResTreeNonNetworked           => 0xFFC0C0FF,
            PredefinedTagAdd              => 0xFF44AA44,
            PredefinedTagRemove           => 0xFF2222AA,
            TemporaryModSettingsTint      => 0x30FF0000,
            NewModTint                    => 0x8000FF00,
            NoTint                        => 0x00000000,
            ChangedItemPreferenceStar     => 0x30FFFFFF,
            OptionColor1                  => 0xFFF8CD8E,
            OptionColor2                  => 0xFFAAD898,
            OptionColor3                  => 0xFF8AD1E6,
            OptionColor4                  => 0xFF6B8CD9,
            OptionColor5                  => 0xFFA38FD9,
            OptionColor6                  => 0xFFDB9DB3,
            OptionColor7                  => 0xFF6A5CC7,
            OptionColor8                  => 0xFF6BB5A6,
            OptionTreeLine                => 0x80FFF0C0,
            GroupLabelBackground          => 0x8A4A4A4A,
            GroupLabelBorder              => 0x80FFF0C0,
            GroupLabelText                => 0xFFFFFFFF,
            GroupLabelBackgroundExpanded  => 0x4F969696,
            GroupLabelBorderExpanded      => 0x80FFF0C0,
            GroupLabelTextExpanded        => 0xFFFFFFFF,
            GroupLabelBackgroundCollapsed => 0x4F969696,
            GroupLabelBorderCollapsed     => 0x80FFF0C0,
            GroupLabelTextCollapsed       => 0xFFFFFFFF,
            OptionBorder                  => 0x80FFF0C0,
            _                             => 0,
        };
}
