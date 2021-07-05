using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Plugin;
using ImGuiNET;
using Penumbra.Mod;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class TabCollections
        {
            private readonly Selector                  _selector;
            private readonly ModManager                _manager;
            private          string                    _collectionNames         = null!;
            private          string                    _collectionNamesWithNone = null!;
            private          ModCollection[]           _collections             = null!;
            private          int                       _currentCollectionIndex  = 0;
            private          int                       _currentForcedIndex      = 0;
            private          int                       _currentDefaultIndex     = 0;
            private readonly Dictionary< string, int > _currentCharacterIndices = new();
            private          string                    _newCollectionName       = string.Empty;
            private          string                    _newCharacterName        = string.Empty;

            private void UpdateNames()
            {
                _collections             = _manager.Collections.Collections.Values.Prepend( ModCollection.Empty ).ToArray();
                _collectionNames         = string.Join( "\0", _collections.Skip( 1 ).Select( c => c.Name ) ) + '\0';
                _collectionNamesWithNone = "None\0" + _collectionNames;
                UpdateIndices();
            }


            private int GetIndex( ModCollection collection )
            {
                var ret = _collections.IndexOf( c => c.Name == collection.Name );
                if( ret < 0 )
                {
                    PluginLog.Error( $"Collection {collection.Name} is not found in collections." );
                    return 0;
                }

                return ret;
            }

            private void UpdateIndex()
                => _currentCollectionIndex = GetIndex( _manager.Collections.CurrentCollection ) - 1;

            private void UpdateForcedIndex()
                => _currentForcedIndex = GetIndex( _manager.Collections.ForcedCollection );

            private void UpdateDefaultIndex()
                => _currentDefaultIndex = GetIndex( _manager.Collections.DefaultCollection );

            private void UpdateCharacterIndices()
            {
                _currentCharacterIndices.Clear();
                foreach( var kvp in _manager.Collections.CharacterCollection )
                {
                    _currentCharacterIndices[ kvp.Key ] = GetIndex( kvp.Value );
                }
            }

            private void UpdateIndices()
            {
                UpdateIndex();
                UpdateDefaultIndex();
                UpdateForcedIndex();
                UpdateCharacterIndices();
            }

            public TabCollections( Selector selector )
            {
                _selector = selector;
                _manager  = Service< ModManager >.Get();
                UpdateNames();
            }

            private void CreateNewCollection( Dictionary< string, ModSettings > settings )
            {
                if( _manager.Collections.AddCollection( _newCollectionName, settings ) )
                {
                    _manager.Collections.SetCurrentCollection( _manager.Collections.Collections[ _newCollectionName ] );
                    UpdateNames();
                }

                _newCollectionName = string.Empty;
            }

            private void DrawNewCollectionInput()
            {
                ImGui.InputTextWithHint( "##New Collection", "New Collection", ref _newCollectionName, 64 );

                var changedStyle = false;
                if( _newCollectionName.Length == 0 )
                {
                    changedStyle = true;
                    ImGui.PushStyleVar( ImGuiStyleVar.Alpha, 0.5f );
                }

                if( ImGui.Button( "Create New Empty Collection" ) && _newCollectionName.Length > 0 )
                {
                    CreateNewCollection( new Dictionary< string, ModSettings >() );
                }

                ImGui.SameLine();
                if( ImGui.Button( "Duplicate Current Collection" ) && _newCollectionName.Length > 0 )
                {
                    CreateNewCollection( _manager.Collections.CurrentCollection.Settings );
                }

                if( changedStyle )
                {
                    ImGui.PopStyleVar();
                }

                if( _manager.Collections.Collections.Count      > 1
                 && _manager.Collections.CurrentCollection.Name != ModCollection.DefaultCollection )
                {
                    ImGui.SameLine();
                    if( ImGui.Button( "Delete Current Collection" ) )
                    {
                        _manager.Collections.RemoveCollection( _manager.Collections.CurrentCollection.Name );
                        UpdateNames();
                    }
                }
            }

            private void DrawCurrentCollectionSelector()
            {
                var index = _currentCollectionIndex;
                var combo = ImGui.Combo( "Current Collection", ref index, _collectionNames );
                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip(
                        "This collection will be modified when using the Installed Mods tab and making changes. It does not apply to anything by itself." );
                }

                if( combo && index != _currentCollectionIndex )
                {
                    _manager.Collections.SetCurrentCollection( _collections[ index + 1 ] );
                    _currentCollectionIndex = index;
                    _selector.ReloadSelection();
                }
            }

            private void DrawDefaultCollectionSelector()
            {
                var index = _currentDefaultIndex;
                if( ImGui.Combo( "##Default Collection", ref index, _collectionNamesWithNone ) && index != _currentDefaultIndex )
                {
                    _manager.Collections.SetDefaultCollection( _collections[ index ] );
                    _currentDefaultIndex = index;
                }

                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip(
                        "Mods in the default collection are loaded for any character that is not explicitly named in the character collections below.\n"
                      + "They also take precedence before the forced collection." );
                }

                ImGui.SameLine();
                ImGui.Dummy( new Vector2( 24, 0 ) );
                ImGui.SameLine();
                ImGui.Text( "Default Collection" );
            }

            private void DrawForcedCollectionSelector()
            {
                var index = _currentForcedIndex;
                if( ImGui.Combo( "##Forced Collection", ref index, _collectionNamesWithNone ) && index != _currentForcedIndex )
                {
                    _manager.Collections.SetForcedCollection( _collections[ index ] );
                    _currentForcedIndex = index;
                }

                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip(
                        "Mods in the forced collection are always loaded if not overwritten by anything in the current or character-based collection.\n"
                      + "Please avoid mixing meta-manipulating mods in Forced and other collections, as this will probably not work correctly." );
                }

                ImGui.SameLine();
                ImGui.Dummy( new Vector2( 24, 0 ) );
                ImGui.SameLine();
                ImGui.Text( "Forced Collection" );
            }

            private void DrawNewCharacterCollection()
            {
                ImGui.InputTextWithHint( "##New Character", "New Character Name", ref _newCharacterName, 32 );

                var changedStyle = false;
                if( _newCharacterName.Length == 0 )
                {
                    changedStyle = true;
                    ImGui.PushStyleVar( ImGuiStyleVar.Alpha, 0.5f );
                }

                ImGui.SameLine();
                if( ImGui.Button( "Create New Character Collection" ) && _newCharacterName.Length > 0 )
                {
                    _manager.Collections.CreateCharacterCollection( _newCharacterName );
                    _currentCharacterIndices[ _newCharacterName ] = 0;
                    _newCharacterName                             = string.Empty;
                }

                if( changedStyle )
                {
                    ImGui.PopStyleVar();
                }
            }


            private void DrawCharacterCollectionSelectors()
            {
                if( !ImGui.BeginChild( "##CollectionChild", AutoFillSize, true ) )
                {
                    return;
                }

                DrawDefaultCollectionSelector();
                DrawForcedCollectionSelector();

                foreach( var name in _manager.Collections.CharacterCollection.Keys.ToArray() )
                {
                    var idx = _currentCharacterIndices[ name ];
                    var tmp = idx;
                    if( ImGui.Combo( $"##{name}collection", ref tmp, _collectionNamesWithNone ) && idx != tmp )
                    {
                        _manager.Collections.SetCharacterCollection( name, _collections[ tmp ] );
                        _currentCharacterIndices[ name ] = tmp;
                    }

                    ImGui.SameLine();
                    ImGui.PushFont( UiBuilder.IconFont );

                    if( ImGui.Button( $"{FontAwesomeIcon.Trash.ToIconString()}##{name}" ) )
                    {
                        _manager.Collections.RemoveCharacterCollection( name );
                    }

                    ImGui.PopFont();

                    ImGui.SameLine();
                    ImGui.Text( name );
                }

                DrawNewCharacterCollection();

                ImGui.EndChild();
            }

            public void Draw()
            {
                if( !ImGui.BeginTabItem( "Collections" ) )
                {
                    return;
                }

                if( !ImGui.BeginChild( "##CollectionHandling", new Vector2( -1, ImGui.GetTextLineHeightWithSpacing() * 6 ), true ) )
                {
                    ImGui.EndTabItem();
                    return;
                }

                DrawCurrentCollectionSelector();

                ImGui.Dummy( new Vector2( 0, 10 ) );
                DrawNewCollectionInput();
                ImGui.EndChild();

                DrawCharacterCollectionSelectors();


                ImGui.EndTabItem();
            }
        }
    }
}