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
    HiddenOptionIndicator,
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
