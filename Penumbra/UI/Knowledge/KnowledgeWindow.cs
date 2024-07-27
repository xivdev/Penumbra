using System.Text.Unicode;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Memory;
using ImGuiNET;
using OtterGui.Services;
using OtterGui.Text;

namespace Penumbra.UI.Knowledge;

/// <summary> Draw the progress information for import. </summary>
public sealed class KnowledgeWindow() : Window("Penumbra Knowledge Window"), IUiService
{
    private readonly IReadOnlyList<IKnowledgeTab> _tabs =
    [
        new RaceCodeTab(),
    ];

    private          IKnowledgeTab? _selected;
    private readonly byte[]         _filterStore = new byte[256];

    private TerminatedByteString _filter = TerminatedByteString.Empty;

    public override void Draw()
    {
        DrawSelector();
        ImUtf8.SameLineInner();
        DrawMain();
    }

    private void DrawSelector()
    {
        using var child = ImUtf8.Child("KnowledgeSelector"u8, new Vector2(250 * ImUtf8.GlobalScale, ImGui.GetContentRegionAvail().Y), true);
        if (!child)
            return;

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        ImUtf8.InputText("##Filter"u8, _filterStore, out _filter, "Filter..."u8);

        foreach (var tab in _tabs)
        {
            if (ImUtf8.Selectable(tab.Name, _selected == tab))
                _selected = tab;
        }
    }

    private void DrawMain()
    {
        using var child = ImUtf8.Child("KnowledgeMain"u8, ImGui.GetContentRegionAvail(), true);
        if (!child || _selected == null)
            return;

        _selected.Draw();
    }
}
