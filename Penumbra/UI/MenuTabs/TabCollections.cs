using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin;
using ImGuiNET;
using Penumbra.Hooks;
using Penumbra.Mod;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class TabCollections
        {
            private readonly Selector   _selector;
            private readonly ModManager _manager;
            private          string[]   _collectionNames   = null!;
            private          int        _currentIndex      = 0;
            private          string     _newCollectionName = string.Empty;

            private void UpdateNames()
                => _collectionNames = _manager.Collections.Values.Select( c => c.Name ).ToArray();

            private void UpdateIndex()
            {
                _currentIndex = _collectionNames.IndexOf( c => c == _manager.CurrentCollection.Name );
                if( _currentIndex < 0 )
                {
                    PluginLog.Error( $"Current Collection {_manager.CurrentCollection.Name} is not found in collections." );
                    _currentIndex = 0;
                }
            }

            public TabCollections( Selector selector )
            {
                _selector = selector;
                _manager  = Service< ModManager >.Get();
                UpdateNames();
                UpdateIndex();
            }


            private void CreateNewCollection( Dictionary< string, ModSettings > settings )
            {
                _manager.AddCollection( _newCollectionName, settings );
                _manager.SetCurrentCollection( _newCollectionName );
                _newCollectionName = string.Empty;
                UpdateNames();
                UpdateIndex();
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
                    CreateNewCollection( _manager.CurrentCollection.Settings );
                }

                if( changedStyle )
                {
                    ImGui.PopStyleVar();
                }

                if( _manager.Collections.Count > 1 )
                {
                    ImGui.SameLine();
                    if( ImGui.Button( "Delete Current Collection" ) )
                    {
                        _manager.RemoveCollection( _manager.CurrentCollection.Name );
                        UpdateNames();
                        UpdateIndex();
                    }
                }
            }

            public void Draw()
            {
                if( !ImGui.BeginTabItem( "Collections" ) )
                {
                    return;
                }

                var index = _currentIndex;
                if( ImGui.Combo( "Current Collection", ref index, _collectionNames, _collectionNames.Length ) )
                {
                    if( index != _currentIndex && _manager.SetCurrentCollection( _collectionNames[ index ] ) )
                    {
                        _currentIndex = index;
                        _selector.ReloadSelection();
                        var resourceManager = Service< GameResourceManagement >.Get();
                        resourceManager.ReloadPlayerResources();
                    }
                }

                ImGui.Dummy( new Vector2( 0, 5 ) );
                DrawNewCollectionInput();

                ImGui.EndTabItem();
            }
        }
    }
}