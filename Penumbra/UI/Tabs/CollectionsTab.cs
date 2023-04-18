using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Enums;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI.Classes;
using Penumbra.UI.CollectionTab;

namespace Penumbra.UI.Tabs;

public sealed class CollectionTree
{
    private readonly CollectionStorage   _collections;
    private readonly ActiveCollections   _active;
    private readonly CollectionSelector2 _selector;
    private readonly ActorService        _actors;
    private readonly TargetManager       _targets;

    private static readonly IReadOnlyList<(string Name, uint Border)>                 Buttons      = CreateButtons();
    private static readonly IReadOnlyList<(CollectionType, bool, bool, string, uint)> AdvancedTree = CreateTree();

    public CollectionTree(CollectionManager manager, CollectionSelector2 selector, ActorService actors,
        TargetManager targets)
    {
        _collections = manager.Storage;
        _active      = manager.Active;
        _selector    = selector;
        _actors      = actors;
        _targets     = targets;
    }

    public void DrawSimple()
    {
        var buttonWidth = new Vector2(200 * ImGuiHelpers.GlobalScale, 2 * ImGui.GetTextLineHeightWithSpacing());
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, Vector2.Zero)
            .Push(ImGuiStyleVar.FrameBorderSize, 1 * ImGuiHelpers.GlobalScale);
        DrawSimpleCollectionButton(CollectionType.Default,                  buttonWidth);
        DrawSimpleCollectionButton(CollectionType.Interface,                buttonWidth);
        DrawSimpleCollectionButton(CollectionType.Yourself,                 buttonWidth);
        DrawSimpleCollectionButton(CollectionType.MalePlayerCharacter,      buttonWidth);
        DrawSimpleCollectionButton(CollectionType.FemalePlayerCharacter,    buttonWidth);
        DrawSimpleCollectionButton(CollectionType.MaleNonPlayerCharacter,   buttonWidth);
        DrawSimpleCollectionButton(CollectionType.FemaleNonPlayerCharacter, buttonWidth);

        var specialWidth = buttonWidth with { X = 275 * ImGuiHelpers.GlobalScale };
        var player       = _actors.AwaitedService.GetCurrentPlayer();
        DrawButton($"Current Character ({(player.IsValid ? player.ToString() : "Unavailable")})", CollectionType.Individual, specialWidth, 0,
            player);
        ImGui.SameLine();

        var target = _actors.AwaitedService.FromObject(_targets.Target, false, true, true);
        DrawButton($"Current Target ({(target.IsValid ? target.ToString() : "Unavailable")})", CollectionType.Individual, specialWidth, 0, target);
        if (_active.Individuals.Count > 0)
        {
            ImGui.TextUnformatted("Currently Active Individual Assignments");
            for (var i = 0; i < _active.Individuals.Count; ++i)
            {
                var (name, ids, coll) = _active.Individuals.Assignments[i];
                DrawButton(name, CollectionType.Individual, buttonWidth, 0, ids[0], coll);

                ImGui.SameLine();
                if (ImGui.GetContentRegionAvail().X < buttonWidth.X + ImGui.GetStyle().ItemSpacing.X + ImGui.GetStyle().WindowPadding.X
                 && i < _active.Individuals.Count - 1)
                    ImGui.NewLine();
            }

            ImGui.NewLine();
        }

        var first = true;

        void Button(CollectionType type)
        {
            var (name, border) = Buttons[(int)type];
            var collection = _active.ByType(type);
            if (collection == null)
                return;

            if (first)
            {
                ImGui.Separator();
                ImGui.TextUnformatted("Currently Active Advanced Assignments");
                first = false;
            }
            DrawButton(name, type, buttonWidth, border, ActorIdentifier.Invalid, collection);
            ImGui.SameLine();
            if (ImGui.GetContentRegionAvail().X < buttonWidth.X + ImGui.GetStyle().ItemSpacing.X + ImGui.GetStyle().WindowPadding.X)
                ImGui.NewLine();
        }

        Button(CollectionType.NonPlayerChild);
        Button(CollectionType.NonPlayerElderly);
        foreach (var race in Enum.GetValues<SubRace>().Skip(1))
        {
            Button(CollectionTypeExtensions.FromParts(race, Gender.Male, false));
            Button(CollectionTypeExtensions.FromParts(race, Gender.Female, false));
            Button(CollectionTypeExtensions.FromParts(race, Gender.Male, true));
            Button(CollectionTypeExtensions.FromParts(race, Gender.Female, true));
        }
    }

    public void DrawAdvanced()
    {
        using var table = ImRaii.Table("##advanced", 4, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg);
        if (!table)
            return;

        using var style = ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, Vector2.Zero)
            .Push(ImGuiStyleVar.FrameBorderSize, 1 * ImGuiHelpers.GlobalScale);
        
        var buttonWidth = new Vector2(150 * ImGuiHelpers.GlobalScale, 2 * ImGui.GetTextLineHeightWithSpacing());
        var dummy = new Vector2(1, 0);

        foreach (var (type, pre, post, name, border) in AdvancedTree)
        {
            ImGui.TableNextColumn();
            if (type is CollectionType.Inactive)
                continue;

            if (pre)
                ImGui.Dummy(dummy);
            DrawAssignmentButton(type, buttonWidth, name, border);
            if (post)
                ImGui.Dummy(dummy);
        }
    }

    private void DrawContext(bool open, ModCollection? collection, CollectionType type, ActorIdentifier identifier, char suffix = 'i')
    {
        var label = $"{type}{identifier}{suffix}";
        if (open)
            ImGui.OpenPopup(label);

        using var context = ImRaii.Popup(label);
        if (context)
        {
            using (var color = ImRaii.PushColor(ImGuiCol.Text, Colors.DiscordColor))
            {
                if (ImGui.MenuItem("Use no mods."))
                    _active.SetCollection(ModCollection.Empty, type, _active.Individuals.GetGroup(identifier));
            }

            if (collection != null)
            {
                using var color = ImRaii.PushColor(ImGuiCol.Text, Colors.RegexWarningBorder);
                if (ImGui.MenuItem("Remove this assignment."))
                    _active.SetCollection(null, type, _active.Individuals.GetGroup(identifier));
            }

            foreach (var coll in _collections)
            {
                if (coll != collection && ImGui.MenuItem($"Use {coll.Name}."))
                    _active.SetCollection(coll, type, _active.Individuals.GetGroup(identifier));
            }
        }
    }

    private bool DrawButton(string text, CollectionType type, Vector2 width, uint borderColor, ActorIdentifier id, ModCollection? collection = null)
    {
        using var group      = ImRaii.Group();
        var       invalid    = type == CollectionType.Individual && !id.IsValid;
        var       redundancy = _active.RedundancyCheck(type, id);
        collection ??= _active.ByType(type, id);
        using var color = ImRaii.PushColor(ImGuiCol.Button,
            collection == null
                ? 0
                : redundancy.Length > 0
                    ? Colors.RedundantColor
                    : collection == _active.Current
                        ? Colors.SelectedColor
                        : collection == ModCollection.Empty
                            ? Colors.RedTableBgTint
                            : ImGui.GetColorU32(ImGuiCol.Button), !invalid)
            .Push(ImGuiCol.Border, borderColor == 0 ? ImGui.GetColorU32(ImGuiCol.TextDisabled) : borderColor);
        using var disabled = ImRaii.Disabled(invalid);
        var       button   = ImGui.Button(text, width) || ImGui.IsItemClicked(ImGuiMouseButton.Right);
        var       hovered  = redundancy.Length > 0 && ImGui.IsItemHovered();
        if (!invalid)
        {
            _selector.DragTarget(type, id);
            var name    = collection == ModCollection.Empty ? "Use No Mods" : collection?.Name ?? "Unassigned";
            var size    = ImGui.CalcTextSize(name);
            var textPos = ImGui.GetItemRectMax() - size - ImGui.GetStyle().FramePadding;
            ImGui.GetWindowDrawList().AddText(textPos, ImGui.GetColorU32(ImGuiCol.Text), name);
            DrawContext(button, collection, type, id);
        }

        if (hovered)
            ImGui.SetTooltip(redundancy);

        return button;
    }

    private void DrawSimpleCollectionButton(CollectionType type, Vector2 width)
    {
        DrawButton(type.ToName(), type, width, 0, ActorIdentifier.Invalid);
        ImGui.SameLine();
        var secondLine = string.Empty;
        foreach (var parent in type.InheritanceOrder())
        {
            var coll = _active.ByType(parent);
            if (coll == null)
                continue;

            secondLine = $"\nWill behave as {parent.ToName()} ({coll.Name}) while unassigned.";
            break;
        }

        ImGui.TextUnformatted(type.ToDescription() + secondLine);
        ImGui.Separator();
    }

    private void DrawAssignmentButton(CollectionType type, Vector2 width, string name, uint color)
        => DrawButton(name, type, width, color, ActorIdentifier.Invalid, _active.ByType(type));

    private static IReadOnlyList<(string Name, uint Border)> CreateButtons()
    {
        var ret = Enum.GetValues<CollectionType>().Select(t => (t.ToName(), 0u)).ToArray();

        foreach (var race in Enum.GetValues<SubRace>().Skip(1))
        {
            var color = race switch
            {
                SubRace.Midlander       => 0xAA5C9FE4u,
                SubRace.Highlander      => 0xAA5C9FE4u,
                SubRace.Wildwood        => 0xAA5C9F49u,
                SubRace.Duskwight       => 0xAA5C9F49u,
                SubRace.Plainsfolk      => 0xAAEF8CB6u,
                SubRace.Dunesfolk       => 0xAAEF8CB6u,
                SubRace.SeekerOfTheSun  => 0xAA8CEFECu,
                SubRace.KeeperOfTheMoon => 0xAA8CEFECu,
                SubRace.Seawolf         => 0xAAEFE68Cu,
                SubRace.Hellsguard      => 0xAAEFE68Cu,
                SubRace.Raen            => 0xAAB5EF8Cu,
                SubRace.Xaela           => 0xAAB5EF8Cu,
                SubRace.Helion          => 0xAAFFFFFFu,
                SubRace.Lost            => 0xAAFFFFFFu,
                SubRace.Rava            => 0xAA607FA7u,
                SubRace.Veena           => 0xAA607FA7u,
                _                       => 0u,
            };

            ret[(int)CollectionTypeExtensions.FromParts(race, Gender.Male,   false)] = ($"♂ {race.ToShortName()}", color);
            ret[(int)CollectionTypeExtensions.FromParts(race, Gender.Female, false)] = ($"♀ {race.ToShortName()}", color);
            ret[(int)CollectionTypeExtensions.FromParts(race, Gender.Male,   true)]  = ($"♂ {race.ToShortName()} (NPC)", color);
            ret[(int)CollectionTypeExtensions.FromParts(race, Gender.Female, true)]  = ($"♀ {race.ToShortName()} (NPC)", color);
        }

        ret[(int)CollectionType.MalePlayerCharacter]      = ("♂ Player", 0);
        ret[(int)CollectionType.FemalePlayerCharacter]    = ("♀ Player", 0);
        ret[(int)CollectionType.MaleNonPlayerCharacter]   = ("♂ NPC", 0);
        ret[(int)CollectionType.FemaleNonPlayerCharacter] = ("♀ NPC", 0);
        return ret;
    }

    private static IReadOnlyList<(CollectionType, bool, bool, string, uint)> CreateTree()
    {
        var ret = new List<(CollectionType, bool, bool, string, uint)>(Buttons.Count);

        void Add(CollectionType type, bool pre, bool post)
        {
            var (name, border) = (int)type >= Buttons.Count ? (type.ToName(), 0) : Buttons[(int)type];
            ret.Add((type, pre, post, name, border));
        }

        Add(CollectionType.Default,                  false, false);
        Add(CollectionType.Interface,                false, false);
        Add(CollectionType.Inactive,                 false, false);
        Add(CollectionType.Inactive,                 false, false);
        Add(CollectionType.Yourself,                 false, true);
        Add(CollectionType.Inactive,                 false, true);
        Add(CollectionType.NonPlayerChild,           false, true);
        Add(CollectionType.NonPlayerElderly,         false, true);
        Add(CollectionType.MalePlayerCharacter,      true,  true);
        Add(CollectionType.FemalePlayerCharacter,    true,  true);
        Add(CollectionType.MaleNonPlayerCharacter,   true,  true);
        Add(CollectionType.FemaleNonPlayerCharacter, true,  true);
        var pre = true;
        foreach (var race in Enum.GetValues<SubRace>().Skip(1))
        {
            Add(CollectionTypeExtensions.FromParts(race, Gender.Male,   false), pre, !pre);
            Add(CollectionTypeExtensions.FromParts(race, Gender.Female, false), pre, !pre);
            Add(CollectionTypeExtensions.FromParts(race, Gender.Male,   true),  pre, !pre);
            Add(CollectionTypeExtensions.FromParts(race, Gender.Female, true),  pre, !pre);
            pre = !pre;
        }

        return ret;
    }
}

public sealed class CollectionPanel
{
    private readonly CollectionManager _manager;
    private readonly ModStorage        _modStorage;
    private readonly InheritanceUi     _inheritanceUi;

    public CollectionPanel(CollectionManager manager, ModStorage modStorage)
    {
        _manager       = manager;
        _modStorage    = modStorage;
        _inheritanceUi = new InheritanceUi(_manager);
    }

    public void Draw()
    {
        var collection = _manager.Active.Current;
        DrawName(collection);
        DrawStatistics(collection);
        _inheritanceUi.Draw();
        DrawSettingsList(collection);
        DrawInactiveSettingsList(collection);
    }

    private void DrawName(ModCollection collection)
    {
        ImGui.TextUnformatted($"{collection.Name} ({collection.AnonymizedName})");
    }

    private void DrawStatistics(ModCollection collection)
    {
        ImGui.TextUnformatted("Used for:");
        var sb = new StringBuilder(128);
        if (_manager.Active.Default == collection)
            sb.Append(CollectionType.Default.ToName()).Append(", ");
        if (_manager.Active.Interface == collection)
            sb.Append(CollectionType.Interface.ToName()).Append(", ");
        foreach (var (type, _) in _manager.Active.SpecialAssignments.Where(p => p.Value == collection))
            sb.Append(type.ToName()).Append(", ");
        foreach (var (name, _) in _manager.Active.Individuals.Where(p => p.Collection == collection))
            sb.Append(name).Append(", ");

        ImGui.SameLine();
        ImGuiUtil.TextWrapped(sb.Length == 0 ? "Nothing" : sb.ToString(0, sb.Length - 2));

        if (collection.DirectParentOf.Count > 0)
        {
            ImGui.TextUnformatted("Inherited by:");
            ImGui.SameLine();
            ImGuiUtil.TextWrapped(string.Join(", ", collection.DirectParentOf.Select(c => c.Name)));
        }
    }

    private void DrawSettingsList(ModCollection collection)
    {
        using var box = ImRaii.ListBox("##activeSettings");
        if (!box)
            return;

        foreach (var (mod, (settings, parent)) in _modStorage.Select(m => (m, collection[m.Index])).Where(t => t.Item2.Settings != null)
                     .OrderBy(t => t.m.Name))
            ImGui.TextUnformatted($"{mod}{(parent != collection ? $" (inherited from {parent.Name})" : string.Empty)}");
    }

    private void DrawInactiveSettingsList(ModCollection collection)
    {
        if (collection.UnusedSettings.Count == 0)
            return;

        if (ImGui.Button("Clear Unused Settings"))
            _manager.Storage.CleanUnavailableSettings(collection);

        using var box = ImRaii.ListBox("##inactiveSettings");
        if (!box)
            return;

        foreach (var name in collection.UnusedSettings.Keys)
            ImGui.TextUnformatted(name);
    }
}

public sealed class CollectionSelector2 : ItemSelector<ModCollection>, IDisposable
{
    private readonly Configuration       _config;
    private readonly CommunicatorService _communicator;
    private readonly CollectionStorage   _storage;
    private readonly ActiveCollections   _active;

    private ModCollection? _dragging;

    public CollectionSelector2(Configuration config, CommunicatorService communicator, CollectionStorage storage, ActiveCollections active)
        : base(new List<ModCollection>(), Flags.Delete | Flags.Add | Flags.Duplicate | Flags.Filter)
    {
        _config       = config;
        _communicator = communicator;
        _storage      = storage;
        _active       = active;

        _communicator.CollectionChange.Subscribe(OnCollectionChange);
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
            ? $"The selected collection {Current.Name} can not be deleted."
            : $"Delete the currently selected collection {Current?.Name}. Hold {_config.DeleteModModifier} to delete.";

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

    protected override bool OnDraw(int idx)
    {
        using var color  = ImRaii.PushColor(ImGuiCol.Header, Colors.SelectedColor);
        var       ret    = ImGui.Selectable(Items[idx].Name, idx == CurrentIdx);
        using var source = ImRaii.DragDropSource();
        if (source)
        {
            _dragging = Items[idx];
            ImGui.SetDragDropPayload("Assignment", nint.Zero, 0);
            ImGui.TextUnformatted($"Assigning {_dragging.Name} to...");
        }

        if (ret)
            _active.SetCollection(Items[idx], CollectionType.Current);

        return ret;
    }

    public void DragTarget(CollectionType type, ActorIdentifier identifier)
    {
        using var target = ImRaii.DragDropTarget();
        if (!target.Success || _dragging == null || !ImGuiUtil.IsDropping("Assignment"))
            return;

        _active.SetCollection(_dragging, type, _active.Individuals.GetGroup(identifier));
        _dragging = null;
    }

    public void Dispose()
    {
        _communicator.CollectionChange.Unsubscribe(OnCollectionChange);
    }

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

public class CollectionsTab : IDisposable, ITab
{
    private readonly CommunicatorService _communicator;
    private readonly Configuration       _configuration;
    private readonly CollectionManager   _collectionManager;
    private readonly CollectionSelector2 _selector;
    private readonly CollectionPanel     _panel;
    private readonly CollectionTree      _tree;

    public enum PanelMode
    {
        SimpleAssignment,
        ComplexAssignment,
        Details,
    };

    public PanelMode Mode = PanelMode.SimpleAssignment;

    public CollectionsTab(CommunicatorService communicator, Configuration configuration, CollectionManager collectionManager,
        ModStorage modStorage, ActorService actors, TargetManager targets)
    {
        _communicator      = communicator;
        _configuration     = configuration;
        _collectionManager = collectionManager;
        _selector          = new CollectionSelector2(_configuration, _communicator, _collectionManager.Storage, _collectionManager.Active);
        _panel             = new CollectionPanel(_collectionManager, modStorage);
        _tree              = new CollectionTree(collectionManager, _selector, actors, targets);
    }

    public void Dispose()
    {
        _selector.Dispose();
    }

    public ReadOnlySpan<byte> Label
        => "Collections"u8;

    public void DrawContent()
    {
        var width = ImGui.CalcTextSize("nnnnnnnnnnnnnnnnnnnnnnnn").X;
        _selector.Draw(width);
        ImGui.SameLine();
        using var group = ImRaii.Group();
        DrawHeaderLine();
        DrawPanel();
    }

    private void DrawHeaderLine()
    {
        using var style      = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 0).Push(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        var       buttonSize = new Vector2(ImGui.GetContentRegionAvail().X / 3f, 0);

        using var _     = ImRaii.Group();
        using var color = ImRaii.PushColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.TabActive), Mode is PanelMode.SimpleAssignment);
        if (ImGui.Button("Simple Assignments", buttonSize))
            Mode = PanelMode.SimpleAssignment;

        ImGui.SameLine();
        color.Pop();
        color.Push(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.TabActive), Mode is PanelMode.Details);
        if (ImGui.Button("Collection Details", buttonSize))
            Mode = PanelMode.Details;

        ImGui.SameLine();
        color.Pop();
        color.Push(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.TabActive), Mode is PanelMode.ComplexAssignment);
        if (ImGui.Button("Advanced Assignments", buttonSize))
            Mode = PanelMode.ComplexAssignment;
    }

    private void DrawPanel()
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        using var child = ImRaii.Child("##CollectionSettings", new Vector2(-1, 0), true, ImGuiWindowFlags.HorizontalScrollbar);
        if (!child)
            return;

        style.Pop();
        switch (Mode)
        {
            case PanelMode.SimpleAssignment:
                _tree.DrawSimple();
                break;
            case PanelMode.ComplexAssignment:
                _tree.DrawAdvanced();
                break;
            case PanelMode.Details:
                _panel.Draw();
                break;
        }

        style.Push(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
    }
}
