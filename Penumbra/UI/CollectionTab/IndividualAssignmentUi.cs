using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Bindings.ImGui;
using ImSharp;
using OtterGui.Custom;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Gui;
using Penumbra.Services;

namespace Penumbra.UI.CollectionTab;

public class IndividualAssignmentUi : IDisposable
{
    private readonly CommunicatorService _communicator;
    private readonly ActorManager        _actors;
    private readonly CollectionManager   _collectionManager;

    private WorldCombo _worldCombo     = null!;
    private NpcCombo   _mountCombo     = null!;
    private NpcCombo   _companionCombo = null!;
    private NpcCombo   _ornamentCombo  = null!;
    private NpcCombo   _bnpcCombo      = null!;
    private NpcCombo   _enpcCombo      = null!;

    private bool _ready;

    public IndividualAssignmentUi(CommunicatorService communicator, ActorManager actors, CollectionManager collectionManager)
    {
        _communicator      = communicator;
        _actors            = actors;
        _collectionManager = collectionManager;
        _communicator.CollectionChange.Subscribe(UpdateIdentifiers, CollectionChange.Priority.IndividualAssignmentUi);
        _actors.Awaiter.ContinueWith(_ => SetupCombos(), TaskScheduler.Default);
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
    private ActorIdentifier[] _playerIdentifiers   = [];
    private ActorIdentifier[] _retainerIdentifiers = [];
    private ActorIdentifier[] _npcIdentifiers      = [];
    private ActorIdentifier[] _ownedIdentifiers    = [];

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
        _worldCombo     = new WorldCombo(_actors.Data.Worlds);
        _mountCombo     = new NpcCombo(new StringU8("##mounts"u8), _actors.Data.Mounts);
        _companionCombo = new NpcCombo(new StringU8("##companions"u8), _actors.Data.Companions);
        _ornamentCombo  = new NpcCombo(new StringU8("##ornaments"u8), _actors.Data.Ornaments);
        _bnpcCombo      = new NpcCombo(new StringU8("##bnpc"u8), _actors.Data.BNpcs);
        _enpcCombo      = new NpcCombo(new StringU8("##enpc"u8), _actors.Data.ENpcs);
        _ready          = true;
    }

    private void UpdateIdentifiers(in CollectionChange.Arguments arguments)
    {
        if (arguments.Type is CollectionType.Individual)
            UpdateIdentifiersInternal();
    }

    private void UpdateIdentifiersInternal()
    {
        var combo = GetNpcCombo(_newKind);
        PlayerTooltip = _collectionManager.Active.Individuals.CanAdd(IdentifierType.Player, _newCharacterName,
                _worldCombo.Selected.Key, ObjectKind.None, [], out _playerIdentifiers) switch
            {
                _ when _newCharacterName.Length == 0       => NewPlayerTooltipEmpty,
                IndividualCollections.AddResult.Invalid    => NewPlayerTooltipInvalid,
                IndividualCollections.AddResult.AlreadySet => AlreadyAssigned,
                _                                          => string.Empty,
            };
        RetainerTooltip =
            _collectionManager.Active.Individuals.CanAdd(IdentifierType.Retainer, _newCharacterName, 0, ObjectKind.None, [],
                    out _retainerIdentifiers) switch
                {
                    _ when _newCharacterName.Length == 0       => NewRetainerTooltipEmpty,
                    IndividualCollections.AddResult.Invalid    => NewRetainerTooltipInvalid,
                    IndividualCollections.AddResult.AlreadySet => AlreadyAssigned,
                    _                                          => string.Empty,
                };
        if (combo.Selected.Ids.Length > 0)
        {
            NpcTooltip = _collectionManager.Active.Individuals.CanAdd(IdentifierType.Npc, string.Empty, ushort.MaxValue, _newKind,
                    combo.Selected.Ids, out _npcIdentifiers) switch
                {
                    IndividualCollections.AddResult.AlreadySet => AlreadyAssigned,
                    _                                          => string.Empty,
                };
            OwnedTooltip = _collectionManager.Active.Individuals.CanAdd(IdentifierType.Owned, _newCharacterName,
                    _worldCombo.Selected.Key, _newKind,
                    combo.Selected.Ids, out _ownedIdentifiers) switch
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
            _npcIdentifiers   = [];
            _ownedIdentifiers = [];
        }
    }
}
