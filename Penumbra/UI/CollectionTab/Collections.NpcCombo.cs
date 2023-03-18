using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using OtterGui.Widgets;

namespace Penumbra.UI.CollectionTab;

public sealed class NpcCombo : FilterComboCache<(string Name, uint[] Ids)>
{
    private readonly string _label;

    public NpcCombo(string label, IReadOnlyDictionary<uint, string> names)
        : base(() => names.GroupBy(kvp => kvp.Value).Select(g => (g.Key, g.Select(g => g.Key).ToArray())).OrderBy(g => g.Key).ToList())
        => _label = label;

    protected override string ToString((string Name, uint[] Ids) obj)
        => obj.Name;

    protected override bool DrawSelectable(int globalIdx, bool selected)
    {
        var (name, ids) = Items[globalIdx];
        var ret = ImGui.Selectable(name, selected);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(string.Join('\n', ids.Select(i => i.ToString())));

        return ret;
    }

    public bool Draw(float width)
        => Draw(_label, CurrentSelection.Name, string.Empty, width, ImGui.GetTextLineHeightWithSpacing());
}
