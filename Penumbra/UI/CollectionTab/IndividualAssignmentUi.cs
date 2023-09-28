using Dalamud.Game.ClientState.Objects.Enums;
using ImGuiNET;
using OtterGui.Custom;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.GameData.Actors;
using Penumbra.Services;

namespace Penumbra.UI.CollectionTab;

public class IndividualAssignmentUi : IDisposable
{
    private readonly CommunicatorService _communicator;
    private readonly ActorService        _actorService;
    private readonly CollectionManager   _collectionManager;

    private WorldCombo _worldCombo     = null!;
    private NpcCombo   _mountCombo     = null!;
    private NpcCombo   _companionCombo = null!;
    private NpcCombo   _ornamentCombo  = null!;
    private NpcCombo   _bnpcCombo      = null!;
    private NpcCombo   _enpcCombo      = null!;

    private bool _ready;

    public IndividualAssignmentUi(CommunicatorService communicator, ActorService actors, CollectionManager collectionManager)
    {
        _communicator      = communicator;
        _actorService      = actors;
        _collectionManager = collectionManager;
        _communicator.CollectionChange.Subscribe(UpdateIdentifiers, CollectionChange.Priority.IndividualAssignmentUi);
        if (_actorService.Valid)
            SetupCombos();
        else
            _actorService.FinishedCreation += SetupCombos;
    }

    public string PlayerTooltip   { get; private set; } = NewPlayerTooltipEmpty;
    public string RetainerTooltip { get; private set; } = NewRetainerTooltipEmpty;
    public string NpcTooltip      { get; private set; } = NewNpcTooltipEmpty;
    public string OwnedTooltip    { get; private set; } = NewPlayerTooltipEmpty;

    public ActorIdentifier[] PlayerIdentifiers
        => _playerIdentifiers;

    public ActorIdentifier[] RetainerIdentifiers
        => _retainerIdentifiers;

    public ActorIdentifier[] NpcIdentifiers
        => _npcIdentifiers;

    public ActorIdentifier[] OwnedIdentifiers
        => _ownedIdentifiers;

    public void DrawWorldCombo(float width)
    {
        if (_ready && _worldCombo.Draw(width))
            UpdateIdentifiersInternal();
    }

    public void DrawObjectKindCombo(float width)
    {
        if (_ready && IndividualHelpers.DrawObjectKindCombo(width, _newKind, out _newKind, ObjectKinds))
            UpdateIdentifiersInternal();
    }

    public void DrawNewPlayerCollection(float width)
    {
        if (!_ready)
            return;

        ImGui.SetNextItemWidth(width);
        if (ImGui.InputTextWithHint("##NewCharacter", "Character Name...", ref _newCharacterName, 32))
            UpdateIdentifiersInternal();
    }

    public void DrawNewNpcCollection(float width)
    {
        if (!_ready)
            return;

        var combo = GetNpcCombo(_newKind);
        if (combo.Draw(width))
            UpdateIdentifiersInternal();
    }

    public void Dispose()
        => _communicator.CollectionChange.Unsubscribe(UpdateIdentifiers);

    // Input Selections.
    private string            _newCharacterName    = string.Empty;
    private ObjectKind        _newKind             = ObjectKind.BattleNpc;
    private ActorIdentifier[] _playerIdentifiers   = Array.Empty<ActorIdentifier>();
    private ActorIdentifier[] _retainerIdentifiers = Array.Empty<ActorIdentifier>();
    private ActorIdentifier[] _npcIdentifiers      = Array.Empty<ActorIdentifier>();
    private ActorIdentifier[] _ownedIdentifiers    = Array.Empty<ActorIdentifier>();

    private const string NewPlayerTooltipEmpty     = "Please enter a valid player name and choose an available world or 'Any World'.";
    private const string NewRetainerTooltipEmpty   = "Please enter a valid retainer name.";
    private const string NewPlayerTooltipInvalid   = "The entered name is not a valid name for a player character.";
    private const string NewRetainerTooltipInvalid = "The entered name is not a valid name for a retainer.";
    private const string AlreadyAssigned           = "The Individual you specified has already been assigned a collection.";
    private const string NewNpcTooltipEmpty        = "Please select a valid NPC from the drop down menu first.";

    private static readonly IReadOnlyList<ObjectKind> ObjectKinds = new[]
    {
        ObjectKind.BattleNpc,
        ObjectKind.EventNpc,
        ObjectKind.Companion,
        ObjectKind.MountType,
        ObjectKind.Ornament,
    };

    private NpcCombo GetNpcCombo(ObjectKind kind)
        => kind switch
        {
            ObjectKind.BattleNpc => _bnpcCombo,
            ObjectKind.EventNpc  => _enpcCombo,
            ObjectKind.MountType => _mountCombo,
            ObjectKind.Companion => _companionCombo,
            ObjectKind.Ornament  => _ornamentCombo,
            _                    => throw new NotImplementedException(),
        };

    /// <summary> Create combos when ready. </summary>
    private void SetupCombos()
    {
        _worldCombo                    =  new WorldCombo(_actorService.AwaitedService.Data.Worlds, Penumbra.Log);
        _mountCombo                    =  new NpcCombo("##mountCombo",     _actorService.AwaitedService.Data.Mounts,     Penumbra.Log);
        _companionCombo                =  new NpcCombo("##companionCombo", _actorService.AwaitedService.Data.Companions, Penumbra.Log);
        _ornamentCombo                 =  new NpcCombo("##ornamentCombo",  _actorService.AwaitedService.Data.Ornaments,  Penumbra.Log);
        _bnpcCombo                     =  new NpcCombo("##bnpcCombo",      _actorService.AwaitedService.Data.BNpcs,      Penumbra.Log);
        _enpcCombo                     =  new NpcCombo("##enpcCombo",      _actorService.AwaitedService.Data.ENpcs,      Penumbra.Log);
        _ready                         =  true;
        _actorService.FinishedCreation -= SetupCombos;
    }

    private void UpdateIdentifiers(CollectionType type, ModCollection? _1, ModCollection? _2, string _3)
    {
        if (type == CollectionType.Individual)
            UpdateIdentifiersInternal();
    }

    private void UpdateIdentifiersInternal()
    {
        var combo = GetNpcCombo(_newKind);
        PlayerTooltip = _collectionManager.Active.Individuals.CanAdd(IdentifierType.Player, _newCharacterName,
                _worldCombo.CurrentSelection.Key, ObjectKind.None,
                Array.Empty<uint>(), out _playerIdentifiers) switch
            {
                _ when _newCharacterName.Length == 0       => NewPlayerTooltipEmpty,
                IndividualCollections.AddResult.Invalid    => NewPlayerTooltipInvalid,
                IndividualCollections.AddResult.AlreadySet => AlreadyAssigned,
                _                                          => string.Empty,
            };
        RetainerTooltip = _collectionManager.Active.Individuals.CanAdd(IdentifierType.Retainer, _newCharacterName, 0, ObjectKind.None,
                Array.Empty<uint>(), out _retainerIdentifiers) switch
            {
                _ when _newCharacterName.Length == 0       => NewRetainerTooltipEmpty,
                IndividualCollections.AddResult.Invalid    => NewRetainerTooltipInvalid,
                IndividualCollections.AddResult.AlreadySet => AlreadyAssigned,
                _                                          => string.Empty,
            };
        if (combo.CurrentSelection.Ids != null)
        {
            NpcTooltip = _collectionManager.Active.Individuals.CanAdd(IdentifierType.Npc, string.Empty, ushort.MaxValue, _newKind,
                    combo.CurrentSelection.Ids, out _npcIdentifiers) switch
                {
                    IndividualCollections.AddResult.AlreadySet => AlreadyAssigned,
                    _                                          => string.Empty,
                };
            OwnedTooltip = _collectionManager.Active.Individuals.CanAdd(IdentifierType.Owned, _newCharacterName,
                    _worldCombo.CurrentSelection.Key, _newKind,
                    combo.CurrentSelection.Ids, out _ownedIdentifiers) switch
                {
                    _ when _newCharacterName.Length == 0       => NewPlayerTooltipEmpty,
                    IndividualCollections.AddResult.Invalid    => NewPlayerTooltipInvalid,
                    IndividualCollections.AddResult.AlreadySet => AlreadyAssigned,
                    _                                          => string.Empty,
                };
        }
        else
        {
            NpcTooltip        = NewNpcTooltipEmpty;
            OwnedTooltip      = NewNpcTooltipEmpty;
            _npcIdentifiers   = Array.Empty<ActorIdentifier>();
            _ownedIdentifiers = Array.Empty<ActorIdentifier>();
        }
    }
}
