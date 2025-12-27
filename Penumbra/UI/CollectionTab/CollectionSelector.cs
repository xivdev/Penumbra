using ImSharp;
using Luna;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.GameData.Actors;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.CollectionTab;

public sealed class CollectionSelector(ActiveCollections active, TutorialService tutorial, IncognitoService incognito) : IPanel
{
    public ReadOnlySpan<byte> Id
        => "##cs"u8;

    private ModCollection? _dragging;

    public record struct Entry(ModCollection Collection, StringU8 Name, StringPair AnonymousName)
    {
        public Entry(ModCollection collection)
            : this(collection,
                collection.Identity.Name.Length > 0 ? new StringU8(collection.Identity.Name) : new StringU8(collection.Identity.AnonymizedName),
                new StringPair(collection.Identity.AnonymizedName))
        { }
    }

    public void DragTargetAssignment(CollectionType type, ActorIdentifier identifier)
    {
        using var target = Im.DragDrop.Target();
        if (!target.Success || _dragging is null || !target.IsDropping("Collection"u8))
            return;

        active.SetCollection(_dragging, type, active.Individuals.GetGroup(identifier));
        _dragging = null;
    }

    public void Draw()
    {
        Im.Cursor.Y += Im.Style.FramePadding.Y;
        var       cache = CacheManager.Instance.GetOrCreateCache<Cache>(Im.Id.Current);
        using var color = ImGuiColor.Header.Push(ColorId.SelectedCollection.Value());
        foreach (var item in cache)
        {
            Im.Cursor.X += Im.Style.FramePadding.X;
            var       ret    = Im.Selectable(incognito.IncognitoMode ? item.AnonymousName : item.Name, active.Current == item.Collection);
            using var source = Im.DragDrop.Source();

            if (active.Current == item.Collection)
                tutorial.OpenTutorial(BasicTutorialSteps.CurrentCollection);

            if (source)
            {
                _dragging = item.Collection;
                source.SetPayload("Collection"u8);
                Im.Text($"Assigning {(incognito.IncognitoMode ? item.AnonymousName : item.Name)} to...");
            }

            if (ret)
                active.SetCollection(item.Collection, CollectionType.Current);
        }
    }

    public sealed class Cache : BasicFilterCache<Entry>, IService
    {
        private readonly CollectionStorage   _collections;
        private readonly CommunicatorService _communicator;

        public Cache(CollectionFilter filter, CollectionStorage collections, CommunicatorService communicator)
            : base(filter)
        {
            _collections  = collections;
            _communicator = communicator;
            _communicator.CollectionChange.Subscribe(OnCollectionChange, CollectionChange.Priority.CollectionSelectorCache);
            _communicator.CollectionRename.Subscribe(OnCollectionRename, CollectionRename.Priority.CollectionSelectorCache);
        }

        private void OnCollectionRename(in CollectionRename.Arguments arguments)
            => Dirty |= IManagedCache.DirtyFlags.Custom;

        private void OnCollectionChange(in CollectionChange.Arguments arguments)
        {
            if (arguments.Type is CollectionType.Inactive)
                Dirty |= IManagedCache.DirtyFlags.Custom;
        }

        protected override IEnumerable<Entry> GetItems()
        {
            yield return new Entry(_collections.DefaultNamed);

            foreach (var collection in _collections.Where(c => c != _collections.DefaultNamed).OrderBy(c => c.Identity.Name))
                yield return new Entry(collection);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _communicator.CollectionChange.Unsubscribe(OnCollectionChange);
            _communicator.CollectionRename.Unsubscribe(OnCollectionRename);
        }
    }
}
