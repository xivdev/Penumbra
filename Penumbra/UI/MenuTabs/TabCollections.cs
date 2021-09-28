using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Logging;
using ImGuiNET;
using Penumbra.Mod;
using Penumbra.Mods;
using Penumbra.UI.Custom;
using Penumbra.Util;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class TabCollections
        {
            public const     string                    LabelCurrentCollection = "Current Collection";
            private readonly Selector                  _selector;
            private readonly ModManager                _manager;
            private          string                    _collectionNames         = null!;
            private          string                    _collectionNamesWithNone = null!;
            private          ModCollection[]           _collections             = null!;
            private          int                       _currentCollectionIndex;
            private          int                       _currentForcedIndex;
            private          int                       _currentDefaultIndex;
            private readonly Dictionary< string, int > _currentCharacterIndices = new();
            private          string                    _newCollectionName       = string.Empty;
            private          string                    _newCharacterName        = string.Empty;

            private void UpdateNames()
            {
                _collections             = _manager.Collections.Collections.Values.Prepend( ModCollection.Empty ).ToArray();
                _collectionNames         = string.Join( "\0", _collections.Skip( 1 ).Select( c => c.Name ) ) + '\0';
                _collectionNamesWithNone = "None\0"                                                          + _collectionNames;
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
                    UpdateNames();
                    SetCurrentCollection( _manager.Collections.Collections[ _newCollectionName ], true );
                }

                _newCollectionName = string.Empty;
            }

            private void DrawCleanCollectionButton()
            {
                if( ImGui.Button( "Clean Settings" ) )
                {
                    var changes = ModFunctions.CleanUpCollection( _manager.Collections.CurrentCollection.Settings,
                        _manager.BasePath.EnumerateDirectories() );
                    _manager.Collections.CurrentCollection.UpdateSettings( changes );
                }

                ImGuiCustom.HoverTooltip(
                    "Remove all stored settings for mods not currently available and fix invalid settings.\nUse at own risk." );
            }

            private void DrawNewCollectionInput()
            {
                ImGui.InputTextWithHint( "##New Collection", "New Collection", ref _newCollectionName, 64 );

                using var style = ImGuiRaii.PushStyle( ImGuiStyleVar.Alpha, 0.5f, _newCollectionName.Length == 0 );

                if( ImGui.Button( "Create New Empty Collection" ) && _newCollectionName.Length > 0 )
                {
                    CreateNewCollection( new Dictionary< string, ModSettings >() );
                }

                ImGui.SameLine();
                if( ImGui.Button( "Duplicate Current Collection" ) && _newCollectionName.Length > 0 )
                {
                    CreateNewCollection( _manager.Collections.CurrentCollection.Settings );
                }

                style.Pop();

                var deleteCondition = _manager.Collections.Collections.Count > 1
                 && _manager.Collections.CurrentCollection.Name              != ModCollection.DefaultCollection;
                ImGui.SameLine();
                if( ImGuiCustom.DisableButton( "Delete Current Collection", deleteCondition ) )
                {
                    _manager.Collections.RemoveCollection( _manager.Collections.CurrentCollection.Name );
                    SetCurrentCollection( _manager.Collections.CurrentCollection, true );
                    UpdateNames();
                }

                if( Penumbra.Config.ShowAdvanced )
                {
                    ImGui.SameLine();
                    DrawCleanCollectionButton();
                }
            }

            private void SetCurrentCollection( int idx, bool force )
            {
                if( !force && idx == _currentCollectionIndex )
                {
                    return;
                }

                _manager.Collections.SetCurrentCollection( _collections[ idx + 1 ] );
                _currentCollectionIndex = idx;
                _selector.Cache.TriggerListReset();
                if( _selector.Mod != null )
                {
                    _selector.SelectModOnUpdate( _selector.Mod.Data.BasePath.Name );
                }
            }

            public void SetCurrentCollection( ModCollection collection, bool force = false )
            {
                var idx = Array.IndexOf( _collections, collection ) - 1;
                if( idx >= 0 )
                {
                    SetCurrentCollection( idx, force );
                }
            }

            public void DrawCurrentCollectionSelector( bool tooltip )
            {
                var index = _currentCollectionIndex;
                var combo = ImGui.Combo( LabelCurrentCollection, ref index, _collectionNames );
                ImGuiCustom.HoverTooltip(
                    "This collection will be modified when using the Installed Mods tab and making changes. It does not apply to anything by itself." );

                if( combo )
                {
                    SetCurrentCollection( index, false );
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

                ImGuiCustom.HoverTooltip(
                    "Mods in the default collection are loaded for any character that is not explicitly named in the character collections below.\n"
                  + "They also take precedence before the forced collection." );

                ImGui.SameLine();
                ImGuiHelpers.ScaledDummy( 24, 0 );
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

                ImGuiCustom.HoverTooltip(
                    "Mods in the forced collection are always loaded if not overwritten by anything in the current or character-based collection.\n"
                  + "Please avoid mixing meta-manipulating mods in Forced and other collections, as this will probably not work correctly." );

                ImGui.SameLine();
                ImGuiHelpers.ScaledDummy( 24, 0 );
                ImGui.SameLine();
                ImGui.Text( "Forced Collection" );
            }

            private void DrawNewCharacterCollection()
            {
                ImGui.InputTextWithHint( "##New Character", "New Character Name", ref _newCharacterName, 32 );

                ImGui.SameLine();
                if( ImGuiCustom.DisableButton( "Create New Character Collection", _newCharacterName.Length > 0 ) )
                {
                    _manager.Collections.CreateCharacterCollection( _newCharacterName );
                    _currentCharacterIndices[ _newCharacterName ] = 0;
                    _newCharacterName                             = string.Empty;
                }

                ImGuiCustom.HoverTooltip(
                    "A character collection will be used whenever you manually redraw a character with the Name you have set up.\n"
                  + "If you enable automatic character redraws in the Settings tab, penumbra will try to use Character collections for corresponding characters automatically.\n" );
            }


            private void DrawCharacterCollectionSelectors()
            {
                using var raii = ImGuiRaii.DeferredEnd( ImGui.EndChild );
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

                    using var font = ImGuiRaii.PushFont( UiBuilder.IconFont );
                    if( ImGui.Button( $"{FontAwesomeIcon.Trash.ToIconString()}##{name}" ) )
                    {
                        _manager.Collections.RemoveCharacterCollection( name );
                    }

                    font.Pop();

                    ImGui.SameLine();
                    ImGui.Text( name );
                }

                DrawNewCharacterCollection();
            }

            public void Draw()
            {
                if( !ImGui.BeginTabItem( "Collections" ) )
                {
                    return;
                }

                using var raii = ImGuiRaii.DeferredEnd( ImGui.EndTabItem )
                   .Push( ImGui.EndChild );

                if( ImGui.BeginChild( "##CollectionHandling", new Vector2( -1, ImGui.GetTextLineHeightWithSpacing() * 6 ), true ) )
                {
                    DrawCurrentCollectionSelector( true );

                    ImGuiHelpers.ScaledDummy( 0, 10 );
                    DrawNewCollectionInput();
                }

                raii.Pop();

                DrawCharacterCollectionSelectors();
            }
        }
    }
}