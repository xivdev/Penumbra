using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using OtterGui.Widgets;

namespace Penumbra.UI.CollectionTab;

public sealed class WorldCombo : FilterComboCache<KeyValuePair<ushort, string>>
{
    private static readonly KeyValuePair<ushort, string> AllWorldPair = new(ushort.MaxValue, "Any World");

    public WorldCombo(IReadOnlyDictionary<ushort, string> worlds)
        : base(worlds.OrderBy(kvp => kvp.Value).Prepend(AllWorldPair))
    {
        CurrentSelection = AllWorldPair;
        CurrentSelectionIdx = 0;
    }

    protected override string ToString(KeyValuePair<ushort, string> obj)
        => obj.Value;

    public bool Draw(float width)
        => Draw("##worldCombo", CurrentSelection.Value, string.Empty, width, ImGui.GetTextLineHeightWithSpacing());
}
