using ImSharp;
using OtterGui;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.GameData.Actors;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.CollectionTab;

public sealed class CollectionSelector : ItemSelector<ModCollection>, IDisposable
{
    private readonly Configuration       _config;
    private readonly CommunicatorService _communicator;
    private readonly CollectionStorage   _storage;
    private readonly ActiveCollections   _active;
    private readonly TutorialService     _tutorial;
    private readonly IncognitoService    _incognito;

    private ModCollection? _dragging;

    public CollectionSelector(Configuration config, CommunicatorService communicator, CollectionStorage storage, ActiveCollections active,
        TutorialService tutorial, IncognitoService incognito)
        : base([], Flags.Delete | Flags.Add | Flags.Duplicate | Flags.Filter)
    {
        _config       = config;
        _communicator = communicator;
        _storage      = storage;
        _active       = active;
        _tutorial     = tutorial;
        _incognito    = incognito;

        _communicator.CollectionChange.Subscribe(OnCollectionChange, CollectionChange.Priority.CollectionSelector);
        // Set items.
        OnCollectionChange(new CollectionChange.Arguments(CollectionType.Inactive, null, null, string.Empty));
        // Set selection.
        OnCollectionChange(new CollectionChange.Arguments(CollectionType.Current, null, _active.Current, string.Empty));
    }

    protected override bool OnDelete(int idx)
    {
        if (idx < 0 || idx >= Items.Count)
            return false;

        // Always return false since we handle the selection update ourselves.
        _storage.RemoveCollection(Items[idx]);
        return false;
    }

    protected override bool DeleteButtonEnabled()
        => _storage.DefaultNamed != Current && _config.DeleteModModifier.IsActive();

    protected override string DeleteButtonTooltip()
        => _storage.DefaultNamed == Current
            ? $"The selected collection {Name(Current)} can not be deleted."
            : $"Delete the currently selected collection {(Current != null ? Name(Current) : string.Empty)}. Hold {_config.DeleteModModifier} to delete.";

    protected override bool OnAdd(string name)
        => _storage.AddCollection(name, null);

    protected override bool OnDuplicate(string name, int idx)
    {
        if (idx < 0 || idx >= Items.Count)
            return false;

        return _storage.AddCollection(name, Items[idx]);
    }

    protected override bool Filtered(int idx)
        => !Items[idx].Identity.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase);

    protected override bool OnDraw(int idx)
    {
        using var color  = ImGuiColor.Header.Push(ColorId.SelectedCollection.Value());
        var       ret    = Im.Selectable(Name(Items[idx]), idx == CurrentIdx);
        using var source = Im.DragDrop.Source();

        if (idx == CurrentIdx)
            _tutorial.OpenTutorial(BasicTutorialSteps.CurrentCollection);

        if (source)
        {
            _dragging = Items[idx];
            source.SetPayload("Collection"u8);
            Im.Text($"Assigning {Name(_dragging)} to...");
        }

        if (ret)
            _active.SetCollection(Items[idx], CollectionType.Current);

        return ret;
    }

    public void DragTargetAssignment(CollectionType type, ActorIdentifier identifier)
    {
        using var target = Im.DragDrop.Target();
        if (!target.Success || _dragging is null || !target.IsDropping("Collection"u8))
            return;

        _active.SetCollection(_dragging, type, _active.Individuals.GetGroup(identifier));
        _dragging = null;
    }

    public void Dispose()
    {
        _communicator.CollectionChange.Unsubscribe(OnCollectionChange);
    }

    private string Name(ModCollection collection)
        => _incognito.IncognitoMode || collection.Identity.Name.Length == 0 ? collection.Identity.AnonymizedName : collection.Identity.Name;

    public void RestoreCollections()
    {
        Items.Clear();
        Items.Add(_storage.DefaultNamed);
        foreach (var c in _storage.OrderBy(c => c.Identity.Name).Where(c => c != _storage.DefaultNamed))
            Items.Add(c);
        SetFilterDirty();
        SetCurrent(_active.Current);
    }

    private void OnCollectionChange(in CollectionChange.Arguments arguments)
    {
        switch (arguments.Type)
        {
            case CollectionType.Temporary: return;
            case CollectionType.Current:
                if (arguments.NewCollection is not null)
                    SetCurrent(arguments.NewCollection);
                SetFilterDirty();
                return;
            case CollectionType.Inactive:
                RestoreCollections();
                SetFilterDirty();
                return;
            default:
                SetFilterDirty();
                return;
        }
    }
}
