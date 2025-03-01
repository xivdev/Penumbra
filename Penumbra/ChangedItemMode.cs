using ImGuiNET;
using OtterGui.Text;

namespace Penumbra;

public enum ChangedItemMode
{
    GroupedCollapsed,
    GroupedExpanded,
    Alphabetical,
}

public static class ChangedItemModeExtensions
{
    public static ReadOnlySpan<byte> ToName(this ChangedItemMode mode)
        => mode switch
        {
            ChangedItemMode.GroupedCollapsed => "Grouped (Collapsed)"u8,
            ChangedItemMode.GroupedExpanded  => "Grouped (Expanded)"u8,
            ChangedItemMode.Alphabetical     => "Alphabetical"u8,
            _                                => "Error"u8,
        };

    public static ReadOnlySpan<byte> ToTooltip(this ChangedItemMode mode)
        => mode switch
        {
            ChangedItemMode.GroupedCollapsed =>
                "Display items as groups by their model and slot. Collapse those groups to a single item by default. Prefers items with more changes affecting them or configured items as the main item."u8,
            ChangedItemMode.GroupedExpanded =>
                "Display items as groups by their model and slot. Expand those groups showing all items by default. Prefers items with more changes affecting them or configured items as the main item."u8,
            ChangedItemMode.Alphabetical => "Display all changed items in a single list sorted alphabetically."u8,
            _                            => ""u8,
        };

    public static bool DrawCombo(ReadOnlySpan<byte> label, ChangedItemMode value, float width, Action<ChangedItemMode> setter)
    {
        ImGui.SetNextItemWidth(width);
        using var combo = ImUtf8.Combo(label, value.ToName());
        if (!combo)
            return false;

        var ret = false;
        foreach (var newValue in Enum.GetValues<ChangedItemMode>())
        {
            var selected = ImUtf8.Selectable(newValue.ToName(), newValue == value);
            if (selected)
            {
                ret = true;
                setter(newValue);
            }

            ImUtf8.HoverTooltip(newValue.ToTooltip());
        }

        return ret;
    }
}
