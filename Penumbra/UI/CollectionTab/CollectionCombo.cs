using ImSharp;
using Luna;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Communication;

namespace Penumbra.UI.CollectionTab;

public sealed class CollectionCombo : SimpleFilterCombo<ModCollection>, IDisposable, IUiService
{
    private readonly Im.ColorDisposable _color = new();
    private readonly Im.StyleDisposable _style = new();
    private readonly CollectionManager  _manager;
    private readonly CollectionChange   _event;

    public CollectionCombo(CollectionManager manager, CollectionChange @event)
        : base(SimpleFilterType.Text)
    {
        _manager = manager;
        _event   = @event;
        Current  = _manager.Active.Current;
        _event.Subscribe(OnCollectionChanged, CollectionChange.Priority.CollectionCombo);
        ClearFilterOnSelection     = true;
        ClearFilterOnCacheDisposal = true;
    }

    private void OnCollectionChanged(in CollectionChange.Arguments arguments)
    {
        if (arguments.Type is not CollectionType.Current)
            return;

        Current = _manager.Active.Current;
    }

    public override StringU8 DisplayString(in ModCollection value)
        => new(value.Identity.Name);

    public override string FilterString(in ModCollection value)
        => value.Identity.Name;

    public override IEnumerable<ModCollection> GetBaseItems()
        => _manager.Storage.OrderBy(c => c.Identity.Name);

    public void Draw(Utf8StringHandler<LabelStringHandlerBuffer> label, float width, Rgba32 color)
    {
        _color.Push(ImGuiColor.FrameBackground, color)
            .Push(ImGuiColor.FrameBackgroundHovered, color);
        if (Draw(label, Current!, "Control and mouse wheel to scroll."u8, width, out var collection))
            _manager.Active.SetCollection(collection, CollectionType.Current);

        _color.Dispose();
        _style.Dispose();
    }

    public void Dispose()
        => _event.Unsubscribe(OnCollectionChanged);

    protected override void PreDrawFilter()
    {
        _color.Dispose();
        _style.PushDefault(ImStyleDouble.ItemSpacing);
        base.PreDrawFilter();
    }
}
