using ImGuiNET;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Actors;

namespace Penumbra.UI.CollectionTab;

public sealed class CollectionCombo(CollectionManager manager, Func<IReadOnlyList<ModCollection>> items)
    : FilterComboCache<ModCollection>(items, MouseWheelType.None, Penumbra.Log)
{
    private readonly ImRaii.Color _color = new();

    protected override void DrawFilter(int currentSelected, float width)
    {
        _color.Dispose();
        base.DrawFilter(currentSelected, width);
    }

    public void Draw(string label, float width, uint color)
    {
        var current = manager.Active.ByType(CollectionType.Current, ActorIdentifier.Invalid);
        _color.Push(ImGuiCol.FrameBg, color).Push(ImGuiCol.FrameBgHovered, color);

        if (Draw(label, current?.Name ?? string.Empty, string.Empty, width, ImGui.GetTextLineHeightWithSpacing()) && CurrentSelection != null)
            manager.Active.SetCollection(CurrentSelection, CollectionType.Current);
        _color.Dispose();
    }

    protected override string ToString(ModCollection obj)
        => obj.Name;
}
