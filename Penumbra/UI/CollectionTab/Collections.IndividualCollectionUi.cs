using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Collections;
using Penumbra.GameData.Actors;
using Penumbra.Services;

namespace Penumbra.UI.CollectionTab;

public class IndividualCollectionUi
{
    private readonly ActorService _actorService;
    private readonly CollectionManager _collectionManager;
    private readonly CollectionSelector _withEmpty;

    public IndividualCollectionUi(ActorService actors, CollectionManager collectionManager, CollectionSelector withEmpty)
    {
        _actorService = actors;
        _collectionManager = collectionManager;
        _withEmpty = withEmpty;
        if (_actorService.Valid)
            SetupCombos();
        else
            _actorService.FinishedCreation += SetupCombos;
    }

    /// <summary> Draw all individual assignments as well as the options to create a new one. </summary>
    public void Draw()
    {
        if (!_ready)
            return;

        using var _ = ImRaii.Group();
        using var mainId = ImRaii.PushId("Individual");
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted($"Individual {TutorialService.ConditionalIndividual}s");
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Individual Collections apply specifically to individual game objects that fulfill the given criteria.\n"
          + $"More general {TutorialService.GroupAssignment} or the {TutorialService.DefaultCollection} do not apply if an Individual Collection takes effect.\n"
          + "Certain related actors - like the ones in cutscenes or preview windows - will try to use appropriate individual collections.");
        ImGui.Separator();
        for (var i = 0; i < _collectionManager.Individuals.Count; ++i)
        {
            DrawIndividualAssignment(i);
        }

        UiHelpers.DefaultLineSpace();
        DrawNewIndividualCollection();
    }

    public void UpdateIdentifiers(CollectionType type, ModCollection? _1, ModCollection? _2, string _3)
    {
        if (type == CollectionType.Individual)
            UpdateIdentifiers();
    }

    // Input Selections.
    private string _newCharacterName = string.Empty;
    private ObjectKind _newKind = ObjectKind.BattleNpc;

    private WorldCombo _worldCombo = null!;
    private NpcCombo _mountCombo = null!;
    private NpcCombo _companionCombo = null!;
    private NpcCombo _ornamentCombo = null!;
    private NpcCombo _bnpcCombo = null!;
    private NpcCombo _enpcCombo = null!;

    private const string NewPlayerTooltipEmpty = "Please enter a valid player name and choose an available world or 'Any World'.";
    private const string NewRetainerTooltipEmpty = "Please enter a valid retainer name.";
    private const string NewPlayerTooltipInvalid = "The entered name is not a valid name for a player character.";
    private const string NewRetainerTooltipInvalid = "The entered name is not a valid name for a retainer.";
    private const string AlreadyAssigned = "The Individual you specified has already been assigned a collection.";
    private const string NewNpcTooltipEmpty = "Please select a valid NPC from the drop down menu first.";

    private ActorIdentifier[] _newPlayerIdentifiers = Array.Empty<ActorIdentifier>();
    private string _newPlayerTooltip = NewPlayerTooltipEmpty;
    private ActorIdentifier[] _newRetainerIdentifiers = Array.Empty<ActorIdentifier>();
    private string _newRetainerTooltip = NewRetainerTooltipEmpty;
    private ActorIdentifier[] _newNpcIdentifiers = Array.Empty<ActorIdentifier>();
    private string _newNpcTooltip = NewNpcTooltipEmpty;
    private ActorIdentifier[] _newOwnedIdentifiers = Array.Empty<ActorIdentifier>();
    private string _newOwnedTooltip = NewPlayerTooltipEmpty;

    private bool _ready;

    /// <summary> Create combos when ready. </summary>
    private void SetupCombos()
    {
        _worldCombo = new WorldCombo(_actorService.AwaitedService.Data.Worlds);
        _mountCombo = new NpcCombo("##mountCombo", _actorService.AwaitedService.Data.Mounts);
        _companionCombo = new NpcCombo("##companionCombo", _actorService.AwaitedService.Data.Companions);
        _ornamentCombo = new NpcCombo("##ornamentCombo", _actorService.AwaitedService.Data.Ornaments);
        _bnpcCombo = new NpcCombo("##bnpcCombo", _actorService.AwaitedService.Data.BNpcs);
        _enpcCombo = new NpcCombo("##enpcCombo", _actorService.AwaitedService.Data.ENpcs);
        _ready = true;
        _actorService.FinishedCreation -= SetupCombos;
    }


    private static readonly IReadOnlyList<ObjectKind> ObjectKinds = new[]
    {
        ObjectKind.BattleNpc,
        ObjectKind.EventNpc,
        ObjectKind.Companion,
        ObjectKind.MountType,
        ObjectKind.Ornament,
    };

    /// <summary> Draw the Object Kind Selector. </summary>
    private bool DrawNewObjectKindOptions(float width)
    {
        ImGui.SetNextItemWidth(width);
        using var combo = ImRaii.Combo("##newKind", _newKind.ToName());
        if (!combo)
            return false;

        var ret = false;
        foreach (var kind in ObjectKinds)
        {
            if (!ImGui.Selectable(kind.ToName(), _newKind == kind))
                continue;

            _newKind = kind;
            ret = true;
        }

        return ret;
    }

    private int _individualDragDropIdx = -1;

    /// <summary> Draw a single individual assignment. </summary>
    private void DrawIndividualAssignment(int idx)
    {
        var (name, _) = _collectionManager.Individuals[idx];
        using var id = ImRaii.PushId(idx);
        _withEmpty.Draw("##IndividualCombo", UiHelpers.InputTextWidth.X, idx);
        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), UiHelpers.IconButtonSize, string.Empty,
                false, true))
            _collectionManager.RemoveIndividualCollection(idx);

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.Selectable(name);
        using (var source = ImRaii.DragDropSource())
        {
            if (source)
            {
                ImGui.SetDragDropPayload("Individual", nint.Zero, 0);
                _individualDragDropIdx = idx;
            }
        }

        using var target = ImRaii.DragDropTarget();
        if (!target.Success || !ImGuiUtil.IsDropping("Individual"))
            return;

        if (_individualDragDropIdx >= 0)
            _collectionManager.MoveIndividualCollection(_individualDragDropIdx, idx);

        _individualDragDropIdx = -1;
    }

    private bool DrawNewPlayerCollection(Vector2 buttonWidth, float width)
    {
        var change = _worldCombo.Draw(width);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(UiHelpers.InputTextWidth.X - ImGui.GetStyle().ItemSpacing.X - width);
        change |= ImGui.InputTextWithHint("##NewCharacter", "Character Name...", ref _newCharacterName, 32);
        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton("Assign Player", buttonWidth, _newPlayerTooltip,
                _newPlayerTooltip.Length > 0 || _newPlayerIdentifiers.Length == 0))
        {
            _collectionManager.CreateIndividualCollection(_newPlayerIdentifiers);
            change = true;
        }

        return change;
    }

    private bool DrawNewNpcCollection(NpcCombo combo, Vector2 buttonWidth, float width)
    {
        var comboWidth = UiHelpers.InputTextWidth.X - ImGui.GetStyle().ItemSpacing.X - width;
        var change = DrawNewObjectKindOptions(width);
        ImGui.SameLine();
        change |= combo.Draw(comboWidth);

        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton("Assign NPC", buttonWidth, _newNpcTooltip,
                _newNpcIdentifiers.Length == 0 || _newNpcTooltip.Length > 0))
        {
            _collectionManager.CreateIndividualCollection(_newNpcIdentifiers);
            change = true;
        }

        return change;
    }

    private bool DrawNewOwnedCollection(Vector2 buttonWidth)
    {
        if (!ImGuiUtil.DrawDisabledButton("Assign Owned NPC", buttonWidth, _newOwnedTooltip,
                _newOwnedIdentifiers.Length == 0 || _newOwnedTooltip.Length > 0))
            return false;

        _collectionManager.CreateIndividualCollection(_newOwnedIdentifiers);
        return true;

    }

    private bool DrawNewRetainerCollection(Vector2 buttonWidth)
    {
        if (!ImGuiUtil.DrawDisabledButton("Assign Bell Retainer", buttonWidth, _newRetainerTooltip,
                _newRetainerIdentifiers.Length == 0 || _newRetainerTooltip.Length > 0))
            return false;

        _collectionManager.CreateIndividualCollection(_newRetainerIdentifiers);
        return true;

    }

    private NpcCombo GetNpcCombo(ObjectKind kind)
        => kind switch
        {
            ObjectKind.BattleNpc => _bnpcCombo,
            ObjectKind.EventNpc => _enpcCombo,
            ObjectKind.MountType => _mountCombo,
            ObjectKind.Companion => _companionCombo,
            ObjectKind.Ornament => _ornamentCombo,
            _ => throw new NotImplementedException(),
        };

    private void DrawNewIndividualCollection()
    {
        var width = (UiHelpers.InputTextWidth.X - 2 * ImGui.GetStyle().ItemSpacing.X) / 3;

        var buttonWidth1 = new Vector2(90 * UiHelpers.Scale, 0);
        var buttonWidth2 = new Vector2(120 * UiHelpers.Scale, 0);

        var assignWidth = new Vector2((UiHelpers.InputTextWidth.X - ImGui.GetStyle().ItemSpacing.X) / 2, 0);
        var change = DrawNewCurrentPlayerCollection(assignWidth);
        ImGui.SameLine();
        change |= DrawNewTargetCollection(assignWidth);

        change |= DrawNewPlayerCollection(buttonWidth1, width);
        ImGui.SameLine();
        change |= DrawNewRetainerCollection(buttonWidth2);

        var combo = GetNpcCombo(_newKind);
        change |= DrawNewNpcCollection(combo, buttonWidth1, width);
        ImGui.SameLine();
        change |= DrawNewOwnedCollection(buttonWidth2);

        if (change)
            UpdateIdentifiers();
    }

    private bool DrawNewCurrentPlayerCollection(Vector2 width)
    {
        var player = _actorService.AwaitedService.GetCurrentPlayer();
        var result = _collectionManager.Individuals.CanAdd(player);
        var tt = result switch
        {
            IndividualCollections.AddResult.Valid => $"Assign a collection to {player}.",
            IndividualCollections.AddResult.AlreadySet => AlreadyAssigned,
            IndividualCollections.AddResult.Invalid => "No logged-in character detected.",
            _ => string.Empty,
        };


        if (!ImGuiUtil.DrawDisabledButton("Assign Current Player", width, tt, result != IndividualCollections.AddResult.Valid))
            return false;

        _collectionManager.CreateIndividualCollection(player);
        return true;

    }

    private bool DrawNewTargetCollection(Vector2 width)
    {
        var target = _actorService.AwaitedService.FromObject(DalamudServices.Targets.Target, false, true, true);
        var result = _collectionManager.Individuals.CanAdd(target);
        var tt = result switch
        {
            IndividualCollections.AddResult.Valid => $"Assign a collection to {target}.",
            IndividualCollections.AddResult.AlreadySet => AlreadyAssigned,
            IndividualCollections.AddResult.Invalid => "No valid character in target detected.",
            _ => string.Empty,
        };
        if (ImGuiUtil.DrawDisabledButton("Assign Current Target", width, tt, result != IndividualCollections.AddResult.Valid))
        {
            _collectionManager.CreateIndividualCollection(_collectionManager.Individuals.GetGroup(target));
            return true;
        }

        ImGui.SameLine();
        ImGuiComponents.HelpMarker(
            "- Bell Retainers also apply to Mannequins named after them, but not to outdoor retainers, since they only carry their owners name.\n"
          + "- Some NPCs are available as Battle- and Event NPCs and need to be setup for both if desired.\n"
          + "- Battle- and Event NPCs may apply to more than one ID if they share the same name. This is language dependent. If you change your clients language, verify that your collections are still correctly assigned.");

        return false;
    }

    private void UpdateIdentifiers()
    {
        var combo = GetNpcCombo(_newKind);
        _newPlayerTooltip = _collectionManager.Individuals.CanAdd(IdentifierType.Player, _newCharacterName,
                _worldCombo.CurrentSelection.Key, ObjectKind.None,
                Array.Empty<uint>(), out _newPlayerIdentifiers) switch
        {
            _ when _newCharacterName.Length == 0 => NewPlayerTooltipEmpty,
            IndividualCollections.AddResult.Invalid => NewPlayerTooltipInvalid,
            IndividualCollections.AddResult.AlreadySet => AlreadyAssigned,
            _ => string.Empty,
        };
        _newRetainerTooltip = _collectionManager.Individuals.CanAdd(IdentifierType.Retainer, _newCharacterName, 0, ObjectKind.None,
                Array.Empty<uint>(), out _newRetainerIdentifiers) switch
        {
            _ when _newCharacterName.Length == 0 => NewRetainerTooltipEmpty,
            IndividualCollections.AddResult.Invalid => NewRetainerTooltipInvalid,
            IndividualCollections.AddResult.AlreadySet => AlreadyAssigned,
            _ => string.Empty,
        };
        if (combo.CurrentSelection.Ids != null)
        {
            _newNpcTooltip = _collectionManager.Individuals.CanAdd(IdentifierType.Npc, string.Empty, ushort.MaxValue, _newKind,
                    combo.CurrentSelection.Ids, out _newNpcIdentifiers) switch
            {
                IndividualCollections.AddResult.AlreadySet => AlreadyAssigned,
                _ => string.Empty,
            };
            _newOwnedTooltip = _collectionManager.Individuals.CanAdd(IdentifierType.Owned, _newCharacterName,
                    _worldCombo.CurrentSelection.Key, _newKind,
                    combo.CurrentSelection.Ids, out _newOwnedIdentifiers) switch
            {
                _ when _newCharacterName.Length == 0 => NewPlayerTooltipEmpty,
                IndividualCollections.AddResult.Invalid => NewPlayerTooltipInvalid,
                IndividualCollections.AddResult.AlreadySet => AlreadyAssigned,
                _ => string.Empty,
            };
        }
        else
        {
            _newNpcTooltip = NewNpcTooltipEmpty;
            _newOwnedTooltip = NewNpcTooltipEmpty;
            _newNpcIdentifiers = Array.Empty<ActorIdentifier>();
            _newOwnedIdentifiers = Array.Empty<ActorIdentifier>();
        }
    }
}
