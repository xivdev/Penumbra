using ImSharp;
using Luna;
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
        Im.Line.SameInner();
        DrawMain();
    }

    private void DrawSelector()
    {
        using var group = Im.Group();
        using (ImStyleSingle.FrameRounding.Push(0).Push(ImStyleDouble.ItemSpacing, Vector2.Zero))
        {
            Im.Item.SetNextWidthScaled(200);
            if (Im.Input.Text("##Filter"u8, _filterStore, out ulong length, "Filter..."u8))
                _lower = ByteString.FromSpanUnsafe(_filterStore.AsSpan(0, (int)length), true, null, null).AsciiToLowerClone();
        }

        using var child = Im.Child.Begin("KnowledgeSelector"u8, Im.ContentRegion.Available with { X = 200 * Im.Style.GlobalScale }, true);
        if (!child)
            return;

        foreach (var tab in _tabs)
        {
            if (!_lower.IsEmpty && tab.SearchTags.IndexOf(_lower.Span) < 0)
                continue;

            if (Im.Selectable(tab.Name, _selected == tab))
                _selected = tab;
        }
    }

    private void DrawMain()
    {
        using var group = Im.Group();
        using (ImStyleSingle.FrameRounding.Push(0).Push(ImStyleDouble.ItemSpacing, Vector2.Zero))
        {
            ImEx.TextFramed(_selected == null ? "No Selection"u8 : _selected.Name, Im.ContentRegion.Available with { Y = 0 });
        }

        using var child = Im.Child.Begin("KnowledgeMain"u8, Im.ContentRegion.Available, true);
        if (!child || _selected == null)
            return;

        _selected.Draw();
    }
}
