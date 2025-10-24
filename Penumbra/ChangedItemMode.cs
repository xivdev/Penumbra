using ImSharp;
using Luna.Generators;

namespace Penumbra;

[NamedEnum(Unknown: "Error")]
[TooltipEnum]
public enum ChangedItemMode
{
    [Name("Grouped (Collapsed)")]
    [Tooltip(
        "Display items as groups by their model and slot. Collapse those groups to a single item by default. Prefers items with more changes affecting them or configured items as the main item.")]
    GroupedCollapsed,

    [Name("Grouped (Expanded)")]
    [Tooltip(
        "Display items as groups by their model and slot. Expand those groups showing all items by default. Prefers items with more changes affecting them or configured items as the main item.")]
    GroupedExpanded,

    [Name("Alphabetical")]
    [Tooltip("Display all changed items in a single list sorted alphabetically.")]
    Alphabetical,
}

public static partial class ChangedItemModeExtensions
{
    private static readonly ChangedItemModeCombo Combo = new();

    private sealed class ChangedItemModeCombo() : SimpleFilterCombo<ChangedItemMode>(SimpleFilterType.Text)
    {
        public override StringU8 DisplayString(in ChangedItemMode value)
            => new(value.ToNameU8());

        public override string FilterString(in ChangedItemMode value)
            => value.ToName();

        public override StringU8 Tooltip(in ChangedItemMode value)
            => new(value.Tooltip());

        public override IEnumerable<ChangedItemMode> GetBaseItems()
            => Enum.GetValues<ChangedItemMode>();
    }

    public static bool DrawCombo(ReadOnlySpan<byte> label, ChangedItemMode value, float width, Action<ChangedItemMode> setter)
    {
        if (!Combo.Draw(label, ref value, StringU8.Empty, width))
            return false;

        setter(value);
        return true;
    }
}
