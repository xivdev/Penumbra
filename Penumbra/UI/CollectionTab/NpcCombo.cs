using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Utility;
using ImGuiNET;
using OtterGui.Widgets;

namespace Penumbra.UI.CollectionTab;

public sealed class NpcCombo : FilterComboCache<(string Name, uint[] Ids)>
{
    private readonly string _label;

    public NpcCombo(string label, IReadOnlyDictionary<uint, string> names)
        : base(() => names.GroupBy(kvp => kvp.Value).Select(g => (g.Key, g.Select(g => g.Key).ToArray())).OrderBy(g => g.Key, Comparer)
            .ToList())
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


    /// <summary> Compare strings in a way that letters and numbers are sorted before any special symbols. </summary>
    private class NameComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (x.IsNullOrEmpty() || y.IsNullOrEmpty())
                return StringComparer.OrdinalIgnoreCase.Compare(x, y);

            return (char.IsAsciiLetterOrDigit(x[0]), char.IsAsciiLetterOrDigit(y[0])) switch
            {
                (true, false) => -1,
                (false, true) => 1,
                _             => StringComparer.OrdinalIgnoreCase.Compare(x, y),
            };
        }
    }

    private static readonly NameComparer Comparer = new();
}
