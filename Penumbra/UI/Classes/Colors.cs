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
}

public static class Colors
{
    public const uint PressEnterWarningBg = 0xFF202080;
    public const uint RegexWarningBorder  = 0xFF0000B0;

    public static (uint DefaultColor, string Name, string Description) Data( this ColorId color )
        => color switch
        {
            // @formatter:off
            ColorId.EnabledMod           => ( 0xFFFFFFFF, "Enabled Mod",                    "A mod that is enabled by the currently selected collection." ),
            ColorId.DisabledMod          => ( 0xFF686880, "Disabled Mod",                   "A mod that is disabled by the currently selected collection." ),
            ColorId.UndefinedMod         => ( 0xFF808080, "Mod With No Settings",           "A mod that is not configured in the currently selected collection or any of the collections it inherits from, and thus implicitly disabled." ),
            ColorId.InheritedMod         => ( 0xFFD0FFFF, "Mod Enabled By Inheritance",     "A mod that is not configured in the currently selected collection, but enabled in a collection it inherits from." ),
            ColorId.InheritedDisabledMod => ( 0xFF688080, "Mod Disabled By Inheritance",    "A mod that is not configured in the currently selected collection, but disabled in a collection it inherits from."),
            ColorId.NewMod               => ( 0xFF66DD66, "New Mod",                        "A mod that was newly imported or created during this session and has not been enabled yet." ),
            ColorId.ConflictingMod       => ( 0xFFAAAAFF, "Mod With Unresolved Conflicts",  "An enabled mod that has conflicts with another enabled mod on the same priority level." ),
            ColorId.HandledConflictMod   => ( 0xFFD0FFD0, "Mod With Resolved Conflicts",    "An enabled mod that has conflicts with another enabled mod on a different priority level." ),
            ColorId.FolderExpanded       => ( 0xFFFFF0C0, "Expanded Mod Folder",            "A mod folder that is currently expanded." ),
            ColorId.FolderCollapsed      => ( 0xFFFFF0C0, "Collapsed Mod Folder",           "A mod folder that is currently collapsed." ),
            ColorId.FolderLine           => ( 0xFFFFF0C0, "Expanded Mod Folder Line",       "The line signifying which descendants belong to an expanded mod folder." ),
            ColorId.ItemId               => ( 0xFF808080, "Item Id",                        "The numeric model id of the given item to the right of changed items." ),
            _                            => throw new ArgumentOutOfRangeException( nameof( color ), color, null ),
            // @formatter:on
        };

    public static uint Value( this ColorId color )
        => Penumbra.Config.Colors.TryGetValue( color, out var value ) ? value : color.Data().DefaultColor;
}