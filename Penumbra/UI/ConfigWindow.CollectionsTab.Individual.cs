using System.Collections.Generic;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Collections;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using OtterGui.Widgets;
using Penumbra.GameData.Actors;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    private partial class CollectionsTab
    {
        private sealed class WorldCombo : FilterComboCache< KeyValuePair< ushort, string > >
        {
            private static readonly KeyValuePair< ushort, string > AllWorldPair = new(ushort.MaxValue, "All Worlds");

            public WorldCombo( IReadOnlyDictionary< ushort, string > worlds )
                : base( worlds.OrderBy( kvp => kvp.Value ).Prepend( AllWorldPair ) )
            {
                CurrentSelection    = AllWorldPair;
                CurrentSelectionIdx = 0;
            }

            protected override string ToString( KeyValuePair< ushort, string > obj )
                => obj.Value;

            public void Draw( float width )
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

            public void Draw( float width )
                => Draw( _label, CurrentSelection.Name, width, ImGui.GetTextLineHeightWithSpacing() );
        }


        // Input Selections.
        private string         _newCharacterName = string.Empty;
        private IdentifierType _newType          = IdentifierType.Player;
        private ObjectKind     _newKind          = ObjectKind.BattleNpc;

        private readonly WorldCombo _worldCombo     = new(Penumbra.Actors.Worlds);
        private readonly NpcCombo   _mountCombo     = new("##mountCombo", Penumbra.Actors.Mounts);
        private readonly NpcCombo   _companionCombo = new("##companionCombo", Penumbra.Actors.Companions);
        private readonly NpcCombo   _bnpcCombo      = new("##bnpcCombo", Penumbra.Actors.BNpcs);
        private readonly NpcCombo   _enpcCombo      = new("##enpcCombo", Penumbra.Actors.ENpcs);

        private void DrawNewIdentifierOptions( float width )
        {
            ImGui.SetNextItemWidth( width );
            using var combo = ImRaii.Combo( "##newType", _newType.ToString() );
            if( combo )
            {
                if( ImGui.Selectable( IdentifierType.Player.ToString(), _newType == IdentifierType.Player ) )
                {
                    _newType = IdentifierType.Player;
                }

                if( ImGui.Selectable( IdentifierType.Owned.ToString(), _newType == IdentifierType.Owned ) )
                {
                    _newType = IdentifierType.Owned;
                }

                if( ImGui.Selectable( IdentifierType.Npc.ToString(), _newType == IdentifierType.Npc ) )
                {
                    _newType = IdentifierType.Npc;
                }
            }
        }

        private void DrawNewObjectKindOptions( float width )
        {
            ImGui.SetNextItemWidth( width );
            using var combo = ImRaii.Combo( "##newKind", _newKind.ToString() );
            if( combo )
            {
                if( ImGui.Selectable( ObjectKind.BattleNpc.ToString(), _newKind == ObjectKind.BattleNpc ) )
                {
                    _newKind = ObjectKind.BattleNpc;
                }

                if( ImGui.Selectable( ObjectKind.EventNpc.ToString(), _newKind == ObjectKind.EventNpc ) )
                {
                    _newKind = ObjectKind.EventNpc;
                }

                if( ImGui.Selectable( ObjectKind.Companion.ToString(), _newKind == ObjectKind.Companion ) )
                {
                    _newKind = ObjectKind.Companion;
                }

                if( ImGui.Selectable( ObjectKind.MountType.ToString(), _newKind == ObjectKind.MountType ) )
                {
                    _newKind = ObjectKind.MountType;
                }
            }
        }

        // We do not check for valid character names.
        private void DrawNewCharacterCollection()
        {
            const string description = "Character Collections apply specifically to individual game objects of the given name.\n"
              + $"More general {GroupAssignment} or the {DefaultCollection} do not apply if an .\n"
              + "Certain actors - like the ones in cutscenes or preview windows - will try to use appropriate character collections.\n";

            var width = ( _window._inputTextWidth.X - 2 * ImGui.GetStyle().ItemSpacing.X ) / 3;
            DrawNewIdentifierOptions( width );
            ImGui.SameLine();
            using( var dis = ImRaii.Disabled( _newType == IdentifierType.Npc ) )
            {
                _worldCombo.Draw( width );
            }

            ImGui.SameLine();

            using( var dis = ImRaii.Disabled( _newType == IdentifierType.Player ) )
            {
                DrawNewObjectKindOptions( width );
            }

            ImGui.SetNextItemWidth( _window._inputTextWidth.X );
            using( var dis = ImRaii.Disabled( _newType == IdentifierType.Npc ) )
            {
                ImGui.InputTextWithHint( "##NewCharacter", "Character Name...", ref _newCharacterName, 32 );
            }

            ImGui.SameLine();
            var disabled = _newCharacterName.Length == 0;
            var tt = disabled
                ? $"Please enter the name of a {ConditionalIndividual} before assigning the collection.\n\n" + description
                : description;
            if( ImGuiUtil.DrawDisabledButton( $"Assign {ConditionalIndividual}", new Vector2( 120 * ImGuiHelpers.GlobalScale, 0 ), tt,
                   disabled ) )
            {
                Penumbra.CollectionManager.CreateCharacterCollection( _newCharacterName );
                _newCharacterName = string.Empty;
            }

            using( var dis = ImRaii.Disabled( _newType == IdentifierType.Player ) )
            {
                switch( _newKind )
                {
                    case ObjectKind.BattleNpc:
                        _bnpcCombo.Draw( _window._inputTextWidth.X );
                        break;
                    case ObjectKind.EventNpc:
                        _enpcCombo.Draw( _window._inputTextWidth.X );
                        break;
                    case ObjectKind.Companion:
                        _companionCombo.Draw( _window._inputTextWidth.X );
                        break;
                    case ObjectKind.MountType:
                        _mountCombo.Draw( _window._inputTextWidth.X );
                        break;
                }
            }
        }

        private void DrawIndividualAssignments()
        {
            using var _ = ImRaii.Group();
            ImGui.TextUnformatted( $"Individual {ConditionalIndividual}s" );
            ImGui.Separator();
            foreach( var name in Penumbra.CollectionManager.Characters.Keys.OrderBy( k => k ).ToArray() )
            {
                using var id = ImRaii.PushId( name );
                DrawCollectionSelector( string.Empty, _window._inputTextWidth.X, CollectionType.Character, true, name );
                ImGui.SameLine();
                if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Trash.ToIconString(), _window._iconButtonSize, string.Empty,
                       false, true ) )
                {
                    Penumbra.CollectionManager.RemoveCharacterCollection( name );
                }

                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted( name );
            }

            ImGui.Dummy( Vector2.Zero );
            DrawNewCharacterCollection();
        }
    }
}