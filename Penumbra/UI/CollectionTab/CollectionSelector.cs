using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
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

    private ModCollection? _dragging;

    public bool IncognitoMode;

    public CollectionSelector(Configuration config, CommunicatorService communicator, CollectionStorage storage, ActiveCollections active,
        TutorialService tutorial)
        : base(new List<ModCollection>(), Flags.Delete | Flags.Add | Flags.Duplicate | Flags.Filter)
    {
        _config       = config;
        _communicator = communicator;
        _storage      = storage;
        _active       = active;
        _tutorial     = tutorial;

        _communicator.CollectionChange.Subscribe(OnCollectionChange, CollectionChange.Priority.CollectionSelector);
        // Set items.
        OnCollectionChange(CollectionType.Inactive, null, null, string.Empty);
        // Set selection.
        OnCollectionChange(CollectionType.Current, null, _active.Current, string.Empty);
    }

    protected override bool OnDelete(int idx)
    {
        if (idx < 0 || idx >= Items.Count)
            return false;

        return _storage.RemoveCollection(Items[idx]);
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
        => !Items[idx].Name.Contains(Filter, StringComparison.OrdinalIgnoreCase);

    private const string PayloadString = "Collection";

    protected override bool OnDraw(int idx)
    {
        using var color  = ImRaii.PushColor(ImGuiCol.Header, ColorId.SelectedCollection.Value());
        var       ret    = ImGui.Selectable(Name(Items[idx]), idx == CurrentIdx);
        using var source = ImRaii.DragDropSource();

        if (idx == CurrentIdx)
            _tutorial.OpenTutorial(BasicTutorialSteps.CurrentCollection);

        if (source)
        {
            _dragging = Items[idx];
            ImGui.SetDragDropPayload(PayloadString, nint.Zero, 0);
            ImGui.TextUnformatted($"Assigning {Name(_dragging)} to...");
        }

        if (ret)
            _active.SetCollection(Items[idx], CollectionType.Current);

        return ret;
    }

    public void DragTargetAssignment(CollectionType type, ActorIdentifier identifier)
    {
        using var target = ImRaii.DragDropTarget();
        if (!target.Success || _dragging == null || !ImGuiUtil.IsDropping(PayloadString))
            return;

        _active.SetCollection(_dragging, type, _active.Individuals.GetGroup(identifier));
        _dragging = null;
    }

    public void Dispose()
    {
        _communicator.CollectionChange.Unsubscribe(OnCollectionChange);
    }

    private string Name(ModCollection collection)
        => IncognitoMode ? collection.AnonymizedName : collection.Name;

    private void OnCollectionChange(CollectionType type, ModCollection? old, ModCollection? @new, string _3)
    {
        switch (type)
        {
            case CollectionType.Temporary: return;
            case CollectionType.Current:
                if (@new != null)
                    SetCurrent(@new);
                SetFilterDirty();
                return;
            case CollectionType.Inactive:
                Items.Clear();
                foreach (var c in _storage.OrderBy(c => c.Name))
                    Items.Add(c);

                if (old == Current)
                    ClearCurrentSelection();
                else
                    TryRestoreCurrent();
                SetFilterDirty();
                return;
            default:
                SetFilterDirty();
                return;
        }
    }
}
