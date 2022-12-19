using System;
using System.Collections.Generic;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Collections;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface.Components;
using OtterGui.Widgets;
using Penumbra.GameData.Actors;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    private partial class CollectionsTab
    {
        private sealed class WorldCombo : FilterComboCache< KeyValuePair< ushort, string > >
        {
            private static readonly KeyValuePair< ushort, string > AllWorldPair = new(ushort.MaxValue, "Any World");

            public WorldCombo( IReadOnlyDictionary< ushort, string > worlds )
                : base( worlds.OrderBy( kvp => kvp.Value ).Prepend( AllWorldPair ) )
            {
                CurrentSelection    = AllWorldPair;
                CurrentSelectionIdx = 0;
            }

            protected override string ToString( KeyValuePair< ushort, string > obj )
                => obj.Value;

            public bool Draw( float width )
                => Draw( "##worldCombo", CurrentSelection.Value, width, ImGui.GetTextLineHeightWithSpacing() );
        }

        private sealed class NpcCombo : FilterComboCache< (string Name, uint[] Ids) >
        {
            private readonly string _label;

            public NpcCombo( string label, IReadOnlyDictionary< uint, string > names )
                : base( () => names.GroupBy( kvp => kvp.Value ).Select( g => ( g.Key, g.Select( g => g.Key ).ToArray() ) ).OrderBy( g => g.Key ).ToList() )
                => _label = label;

            protected override string ToString( (string Name, uint[] Ids) obj )
                => obj.Name;

            protected override bool DrawSelectable( int globalIdx, bool selected )
            {
                var (name, ids) = Items[ globalIdx ];
                var ret = ImGui.Selectable( name, selected );
                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip( string.Join( '\n', ids.Select( i => i.ToString() ) ) );
                }

                return ret;
            }

            public bool Draw( float width )
                => Draw( _label, CurrentSelection.Name, width, ImGui.GetTextLineHeightWithSpacing() );
        }


        // Input Selections.
        private string     _newCharacterName = string.Empty;
        private ObjectKind _newKind          = ObjectKind.BattleNpc;

        private readonly WorldCombo _worldCombo     = new(Penumbra.Actors.Data.Worlds);
        private readonly NpcCombo   _mountCombo     = new("##mountCombo", Penumbra.Actors.Data.Mounts);
        private readonly NpcCombo   _companionCombo = new("##companionCombo", Penumbra.Actors.Data.Companions);
        private readonly NpcCombo   _ornamentCombo  = new("##ornamentCombo", Penumbra.Actors.Data.Ornaments);
        private readonly NpcCombo   _bnpcCombo      = new("##bnpcCombo", Penumbra.Actors.Data.BNpcs);
        private readonly NpcCombo   _enpcCombo      = new("##enpcCombo", Penumbra.Actors.Data.ENpcs);

        private const string NewPlayerTooltipEmpty     = "Please enter a valid player name and choose an available world or 'Any World'.";
        private const string NewRetainerTooltipEmpty   = "Please enter a valid retainer name.";
        private const string NewPlayerTooltipInvalid   = "The entered name is not a valid name for a player character.";
        private const string NewRetainerTooltipInvalid = "The entered name is not a valid name for a retainer.";
        private const string AlreadyAssigned           = "The Individual you specified has already been assigned a collection.";
        private const string NewNpcTooltipEmpty        = "Please select a valid NPC from the drop down menu first.";

        private ActorIdentifier[] _newPlayerIdentifiers   = Array.Empty< ActorIdentifier >();
        private string            _newPlayerTooltip       = NewPlayerTooltipEmpty;
        private ActorIdentifier[] _newRetainerIdentifiers = Array.Empty< ActorIdentifier >();
        private string            _newRetainerTooltip     = NewRetainerTooltipEmpty;
        private ActorIdentifier[] _newNpcIdentifiers      = Array.Empty< ActorIdentifier >();
        private string            _newNpcTooltip          = NewNpcTooltipEmpty;
        private ActorIdentifier[] _newOwnedIdentifiers    = Array.Empty< ActorIdentifier >();
        private string            _newOwnedTooltip        = NewPlayerTooltipEmpty;

        private bool DrawNewObjectKindOptions( float width )
        {
            ImGui.SetNextItemWidth( width );
            using var combo = ImRaii.Combo( "##newKind", _newKind.ToName() );
            if( !combo )
            {
                return false;
            }

            var ret = false;
            foreach( var kind in new[] { ObjectKind.BattleNpc, ObjectKind.EventNpc, ObjectKind.Companion, ObjectKind.MountType, ( ObjectKind )15 } ) // TODO: CS Update
            {
                if( ImGui.Selectable( kind.ToName(), _newKind == kind ) )
                {
                    _newKind = kind;
                    ret      = true;
                }
            }

            return ret;
        }

        private int _individualDragDropIdx = -1;

        private void DrawIndividualAssignments()
        {
            using var _      = ImRaii.Group();
            using var mainId = ImRaii.PushId( "Individual" );
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted( $"Individual {ConditionalIndividual}s" );
            ImGui.SameLine();
            ImGuiComponents.HelpMarker( "Individual Collections apply specifically to individual game objects that fulfill the given criteria.\n"
              + $"More general {GroupAssignment} or the {DefaultCollection} do not apply if an Individual Collection takes effect.\n"
              + "Certain related actors - like the ones in cutscenes or preview windows - will try to use appropriate individual collections." );
            ImGui.Separator();
            for( var i = 0; i < Penumbra.CollectionManager.Individuals.Count; ++i )
            {
                var (name, _) = Penumbra.CollectionManager.Individuals[ i ];
                using var id = ImRaii.PushId( i );
                CollectionsWithEmpty.Draw( string.Empty, _window._inputTextWidth.X, i );
                ImGui.SameLine();
                if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Trash.ToIconString(), _window._iconButtonSize, string.Empty,
                       false, true ) )
                {
                    Penumbra.CollectionManager.RemoveIndividualCollection( i );
                }

                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.Selectable( name );
                using( var source = ImRaii.DragDropSource() )
                {
                    if( source )
                    {
                        ImGui.SetDragDropPayload( "Individual", IntPtr.Zero, 0 );
                        _individualDragDropIdx = i;
                    }
                }

                using( var target = ImRaii.DragDropTarget() )
                {
                    if( !target.Success || !ImGuiUtil.IsDropping( "Individual" ) )
                    {
                        continue;
                    }

                    if( _individualDragDropIdx >= 0 )
                    {
                        Penumbra.CollectionManager.MoveIndividualCollection( _individualDragDropIdx, i );
                    }

                    _individualDragDropIdx = -1;
                }
            }

            ImGui.Dummy( _window._defaultSpace );
            DrawNewIndividualCollection();
        }

        private bool DrawNewPlayerCollection( Vector2 buttonWidth, float width )
        {
            var change = _worldCombo.Draw( width );
            ImGui.SameLine();
            ImGui.SetNextItemWidth( _window._inputTextWidth.X - ImGui.GetStyle().ItemSpacing.X - width );
            change |= ImGui.InputTextWithHint( "##NewCharacter", "Character Name...", ref _newCharacterName, 32 );
            ImGui.SameLine();
            if( ImGuiUtil.DrawDisabledButton( "Assign Player", buttonWidth, _newPlayerTooltip, _newPlayerTooltip.Length > 0 || _newPlayerIdentifiers.Length == 0 ) )
            {
                Penumbra.CollectionManager.CreateIndividualCollection( _newPlayerIdentifiers );
                change = true;
            }

            return change;
        }

        private bool DrawNewNpcCollection( NpcCombo combo, Vector2 buttonWidth, float width )
        {
            var comboWidth = _window._inputTextWidth.X - ImGui.GetStyle().ItemSpacing.X - width;
            var change     = DrawNewObjectKindOptions( width );
            ImGui.SameLine();
            change |= combo.Draw( comboWidth );

            ImGui.SameLine();
            if( ImGuiUtil.DrawDisabledButton( "Assign NPC", buttonWidth, _newNpcTooltip, _newNpcIdentifiers.Length == 0 || _newNpcTooltip.Length > 0 ) )
            {
                Penumbra.CollectionManager.CreateIndividualCollection( _newNpcIdentifiers );
                change = true;
            }

            return change;
        }

        private bool DrawNewOwnedCollection( Vector2 buttonWidth )
        {
            if( ImGuiUtil.DrawDisabledButton( "Assign Owned NPC", buttonWidth, _newOwnedTooltip, _newOwnedIdentifiers.Length == 0 || _newOwnedTooltip.Length > 0 ) )
            {
                Penumbra.CollectionManager.CreateIndividualCollection( _newOwnedIdentifiers );
                return true;
            }

            return false;
        }

        private bool DrawNewRetainerCollection( Vector2 buttonWidth )
        {
            if( ImGuiUtil.DrawDisabledButton( "Assign Bell Retainer", buttonWidth, _newRetainerTooltip, _newRetainerIdentifiers.Length == 0 || _newRetainerTooltip.Length > 0 ) )
            {
                Penumbra.CollectionManager.CreateIndividualCollection( _newRetainerIdentifiers );
                return true;
            }

            return false;
        }

        private NpcCombo GetNpcCombo( ObjectKind kind )
            => kind switch
            {
                ObjectKind.BattleNpc => _bnpcCombo,
                ObjectKind.EventNpc  => _enpcCombo,
                ObjectKind.MountType => _mountCombo,
                ObjectKind.Companion => _companionCombo,
                ( ObjectKind )15     => _ornamentCombo, // TODO: CS update
                _                    => throw new NotImplementedException(),
            };

        private void DrawNewIndividualCollection()
        {
            var width = ( _window._inputTextWidth.X - 2 * ImGui.GetStyle().ItemSpacing.X ) / 3;

            var buttonWidth1 = new Vector2( 90  * ImGuiHelpers.GlobalScale, 0 );
            var buttonWidth2 = new Vector2( 120 * ImGuiHelpers.GlobalScale, 0 );

            var change = DrawNewCurrentPlayerCollection();

            change |= DrawNewPlayerCollection( buttonWidth1, width );
            ImGui.SameLine();
            change |= DrawNewRetainerCollection( buttonWidth2 );

            var combo = GetNpcCombo( _newKind );
            change |= DrawNewNpcCollection( combo, buttonWidth1, width );
            ImGui.SameLine();
            change |= DrawNewOwnedCollection( buttonWidth2 );

            if( change )
            {
                UpdateIdentifiers();
            }
        }

        private bool DrawNewCurrentPlayerCollection()
        {
            var player = Penumbra.Actors.GetCurrentPlayer();
            var result = Penumbra.CollectionManager.Individuals.CanAdd( player );
            var tt = result switch
            {
                IndividualCollections.AddResult.Valid      => $"Assign a collection to {player}.",
                IndividualCollections.AddResult.AlreadySet => AlreadyAssigned,
                IndividualCollections.AddResult.Invalid    => "No logged-in character detected.",
                _                                          => string.Empty,
            };

            if( ImGuiUtil.DrawDisabledButton( "Assign Currently Played Character", _window._inputTextWidth, tt, result != IndividualCollections.AddResult.Valid ) )
            {
                Penumbra.CollectionManager.Individuals.Add( new[] { player }, Penumbra.CollectionManager.Default );
                return true;
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker( "- Bell Retainers also apply to Mannequins named after them, but not to outdoor retainers, since they only carry their owners name.\n"
              + "- Some NPCs are available as Battle- and Event NPCs and need to be setup for both if desired.\n"
              + "- Battle- and Event NPCs may apply to more than one ID if they share the same name. This is language dependent. If you change your clients language, verify that your collections are still correctly assigned." );

            return false;
        }

        private void UpdateIdentifiers()
        {
            var combo = GetNpcCombo( _newKind );
            _newPlayerTooltip = Penumbra.CollectionManager.Individuals.CanAdd( IdentifierType.Player, _newCharacterName, _worldCombo.CurrentSelection.Key, ObjectKind.None,
                    Array.Empty< uint >(), out _newPlayerIdentifiers ) switch
                {
                    _ when _newCharacterName.Length == 0       => NewPlayerTooltipEmpty,
                    IndividualCollections.AddResult.Invalid    => NewPlayerTooltipInvalid,
                    IndividualCollections.AddResult.AlreadySet => AlreadyAssigned,
                    _                                          => string.Empty,
                };
            _newRetainerTooltip = Penumbra.CollectionManager.Individuals.CanAdd( IdentifierType.Retainer, _newCharacterName, _worldCombo.CurrentSelection.Key, ObjectKind.None,
                    Array.Empty< uint >(), out _newRetainerIdentifiers ) switch
                {
                    _ when _newCharacterName.Length == 0       => NewRetainerTooltipEmpty,
                    IndividualCollections.AddResult.Invalid    => NewRetainerTooltipInvalid,
                    IndividualCollections.AddResult.AlreadySet => AlreadyAssigned,
                    _                                          => string.Empty,
                };
            if( combo.CurrentSelection.Ids != null )
            {
                _newNpcTooltip = Penumbra.CollectionManager.Individuals.CanAdd( IdentifierType.Npc, string.Empty, ushort.MaxValue, _newKind,
                        combo.CurrentSelection.Ids, out _newNpcIdentifiers ) switch
                    {
                        IndividualCollections.AddResult.AlreadySet => AlreadyAssigned,
                        _                                          => string.Empty,
                    };
                _newOwnedTooltip = Penumbra.CollectionManager.Individuals.CanAdd( IdentifierType.Owned, _newCharacterName, _worldCombo.CurrentSelection.Key, _newKind,
                        combo.CurrentSelection.Ids, out _newOwnedIdentifiers ) switch
                    {
                        _ when _newCharacterName.Length == 0       => NewPlayerTooltipEmpty,
                        IndividualCollections.AddResult.Invalid    => NewPlayerTooltipInvalid,
                        IndividualCollections.AddResult.AlreadySet => AlreadyAssigned,
                        _                                          => string.Empty,
                    };
            }
            else
            {
                _newNpcTooltip       = NewNpcTooltipEmpty;
                _newOwnedTooltip     = NewNpcTooltipEmpty;
                _newNpcIdentifiers   = Array.Empty< ActorIdentifier >();
                _newOwnedIdentifiers = Array.Empty< ActorIdentifier >();
            }
        }

        private void UpdateIdentifiers( CollectionType type, ModCollection? _1, ModCollection? _2, string _3 )
        {
            if( type == CollectionType.Individual )
            {
                UpdateIdentifiers();
            }
        }
    }
}