using System;

namespace Penumbra.UI.Classes;

public enum ColorId
{
    EnabledMod,
    DisabledMod,
    UndefinedMod,
    InheritedMod,
    InheritedDisabledMod,
    NewMod,
    ConflictingMod,
    HandledConflictMod,
    FolderExpanded,
    FolderCollapsed,
    FolderLine,
    ItemId,
    IncreasedMetaValue,
    DecreasedMetaValue,
}

public static class Colors
{
    public const uint PressEnterWarningBg = 0xFF202080;
    public const uint RegexWarningBorder  = 0xFF0000B0;
    public const uint MetaInfoText        = 0xAAFFFFFF;
    public const uint RedTableBgTint      = 0x40000080;
    public const uint DiscordColor        = 0xFFDA8972;
    public const uint FilterActive        = 0x807070FF;
    public const uint TutorialMarker      = 0xFF20FFFF;
    public const uint TutorialBorder      = 0xD00000FF;
    public const uint ReniColorButton     = 0xFFCC648D;
    public const uint ReniColorHovered    = 0xFFB070B0;
    public const uint ReniColorActive     = 0xFF9070E0;

    public static (uint DefaultColor, string Name, string Description) Data(this ColorId color)
        => color switch
        {
            // @formatter:off
            ColorId.EnabledMod           => ( 0xFFFFFFFF, "Enabled Mod",                       "A mod that is enabled by the currently selected collection." ),
            ColorId.DisabledMod          => ( 0xFF686880, "Disabled Mod",                      "A mod that is disabled by the currently selected collection." ),
            ColorId.UndefinedMod         => ( 0xFF808080, "Mod With No Settings",              "A mod that is not configured in the currently selected collection or any of the collections it inherits from, and thus implicitly disabled." ),
            ColorId.InheritedMod         => ( 0xFFD0FFFF, "Mod Enabled By Inheritance",        "A mod that is not configured in the currently selected collection, but enabled in a collection it inherits from." ),
            ColorId.InheritedDisabledMod => ( 0xFF688080, "Mod Disabled By Inheritance",       "A mod that is not configured in the currently selected collection, but disabled in a collection it inherits from."),
            ColorId.NewMod               => ( 0xFF66DD66, "New Mod",                           "A mod that was newly imported or created during this session and has not been enabled yet." ),
            ColorId.ConflictingMod       => ( 0xFFAAAAFF, "Mod With Unresolved Conflicts",     "An enabled mod that has conflicts with another enabled mod on the same priority level." ),
            ColorId.HandledConflictMod   => ( 0xFFD0FFD0, "Mod With Resolved Conflicts",       "An enabled mod that has conflicts with another enabled mod on a different priority level." ),
            ColorId.FolderExpanded       => ( 0xFFFFF0C0, "Expanded Mod Folder",               "A mod folder that is currently expanded." ),
            ColorId.FolderCollapsed      => ( 0xFFFFF0C0, "Collapsed Mod Folder",              "A mod folder that is currently collapsed." ),
            ColorId.FolderLine           => ( 0xFFFFF0C0, "Expanded Mod Folder Line",          "The line signifying which descendants belong to an expanded mod folder." ),
            ColorId.ItemId               => ( 0xFF808080, "Item Id",                           "The numeric model id of the given item to the right of changed items." ),
            ColorId.IncreasedMetaValue   => ( 0x80008000, "Increased Meta Manipulation Value", "An increased meta manipulation value for floats or an enabled toggle where the default is disabled."),
            ColorId.DecreasedMetaValue   => ( 0x80000080, "Decreased Meta Manipulation Value", "A decreased meta manipulation value for floats or a disabled toggle where the default is enabled."),
            _                            => throw new ArgumentOutOfRangeException( nameof( color ), color, null ),
            // @formatter:on
        };

    public static uint Value(this ColorId color, Configuration config)
        => config.Colors.TryGetValue(color, out var value) ? value : color.Data().DefaultColor;
}
