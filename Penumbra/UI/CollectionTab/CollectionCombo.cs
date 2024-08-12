using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;
using OtterGui.Widgets;
using Penumbra.Collections;
using Penumbra.Collections.Manager;

namespace Penumbra.UI.CollectionTab;

public sealed class CollectionCombo(CollectionManager manager, Func<IReadOnlyList<ModCollection>> items)
    : FilterComboCache<ModCollection>(items, MouseWheelType.Control, Penumbra.Log)
{
    private readonly ImRaii.Color _color = new();

    protected override void DrawFilter(int currentSelected, float width)
    {
        _color.Dispose();
        base.DrawFilter(currentSelected, width);
    }

    public void Draw(string label, float width, uint color)
    {
        var current = manager.Active.Current;
        if (current != CurrentSelection)
        {
            CurrentSelectionIdx = Items.IndexOf(current);
            UpdateSelection(current);
        }

        _color.Push(ImGuiCol.FrameBg, color).Push(ImGuiCol.FrameBgHovered, color);
        if (Draw(label, current.Name, string.Empty, width, ImGui.GetTextLineHeightWithSpacing()) && CurrentSelection != null)
            manager.Active.SetCollection(CurrentSelection, CollectionType.Current);
        _color.Dispose();
    }

    protected override string ToString(ModCollection obj)
        => obj.Name;

    protected override void DrawCombo(string label, string preview, string tooltip, int currentSelected, float previewWidth, float itemHeight,
        ImGuiComboFlags flags)
    {
        base.DrawCombo(label, preview, tooltip, currentSelected, previewWidth, itemHeight, flags);
        ImUtf8.HoverTooltip("Control and mouse wheel to scroll."u8);
    }
}
