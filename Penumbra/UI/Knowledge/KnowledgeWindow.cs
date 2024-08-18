using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.String;

namespace Penumbra.UI.Knowledge;

/// <summary> Draw the progress information for import. </summary>
public sealed class KnowledgeWindow : Window, IUiService
{
    private readonly IReadOnlyList<IKnowledgeTab> _tabs =
    [
        new RaceCodeTab(),
    ];

    private          IKnowledgeTab? _selected;
    private readonly byte[]         _filterStore = new byte[256];

    private ByteString _lower = ByteString.Empty;

    /// <summary> Draw the progress information for import. </summary>
    public KnowledgeWindow()
        : base("Penumbra Knowledge Window")
        => SizeConstraints = new WindowSizeConstraints
        {
            MaximumSize = new Vector2(10000, 10000),
            MinimumSize = new Vector2(400,   200),
        };

    public override void Draw()
    {
        DrawSelector();
        ImUtf8.SameLineInner();
        DrawMain();
    }

    private void DrawSelector()
    {
        using var group = ImUtf8.Group();
        using (ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 0).Push(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
        {
            ImGui.SetNextItemWidth(200 * ImUtf8.GlobalScale);
            if (ImUtf8.InputText("##Filter"u8, _filterStore, out TerminatedByteString filter, "Filter..."u8))
                _lower = ByteString.FromSpanUnsafe(filter, true, null, null).AsciiToLowerClone();
        }

        using var child = ImUtf8.Child("KnowledgeSelector"u8, new Vector2(200 * ImUtf8.GlobalScale, ImGui.GetContentRegionAvail().Y), true);
        if (!child)
            return;

        foreach (var tab in _tabs)
        {
            if (!_lower.IsEmpty && tab.SearchTags.IndexOf(_lower.Span) < 0)
                continue;

            if (ImUtf8.Selectable(tab.Name, _selected == tab))
                _selected = tab;
        }
    }

    private void DrawMain()
    {
        using var group = ImUtf8.Group();
        using (ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 0).Push(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
        {
            ImUtf8.TextFramed(_selected == null ? "No Selection"u8 : _selected.Name, ImGui.GetColorU32(ImGuiCol.FrameBg),
                new Vector2(ImGui.GetContentRegionAvail().X, 0));
        }

        using var child = ImUtf8.Child("KnowledgeMain"u8, ImGui.GetContentRegionAvail(), true);
        if (!child || _selected == null)
            return;

        _selected.Draw();
    }
}
