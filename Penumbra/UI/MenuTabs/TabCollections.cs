using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Logging;
using ImGuiNET;
using Penumbra.Collections;
using Penumbra.Mods;
using Penumbra.UI.Custom;
using Penumbra.Util;

namespace Penumbra.UI;

public partial class SettingsInterface
{
    private class TabCollections
    {
        private const    string                    CharacterCollectionHelpPopup = "Character Collection Information";
        private readonly Selector                  _selector;
        private          string                    _collectionNames         = null!;
        private          string                    _collectionNamesWithNone = null!;
        private          ModCollection[]           _collections             = null!;
        private          int                       _currentCollectionIndex;
        private          int                       _currentDefaultIndex;
        private readonly Dictionary< string, int > _currentCharacterIndices = new();
        private          string                    _newCollectionName       = string.Empty;
        private          string                    _newCharacterName        = string.Empty;

        private void UpdateNames()
        {
            _collections             = Penumbra.CollectionManager.Prepend( ModCollection.Empty ).ToArray();
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
            => _currentCollectionIndex = GetIndex( Penumbra.CollectionManager.Current ) - 1;

        public void UpdateDefaultIndex()
            => _currentDefaultIndex = GetIndex( Penumbra.CollectionManager.Default );

        private void UpdateCharacterIndices()
        {
            _currentCharacterIndices.Clear();
            foreach( var (character, collection) in Penumbra.CollectionManager.Characters )
            {
                _currentCharacterIndices[ character ] = GetIndex( collection );
            }
        }

        private void UpdateIndices()
        {
            UpdateIndex();
            UpdateDefaultIndex();
            UpdateCharacterIndices();
        }

        public TabCollections( Selector selector )
        {
            _selector = selector;
            UpdateNames();
        }

        private void CreateNewCollection( bool duplicate )
        {
            if( Penumbra.CollectionManager.AddCollection( _newCollectionName, duplicate ? Penumbra.CollectionManager.Current : null ) )
            {
                UpdateNames();
                SetCurrentCollection( Penumbra.CollectionManager[ _newCollectionName ]!, true );
            }

            _newCollectionName = string.Empty;
        }

        private static void DrawCleanCollectionButton()
        {
            if( ImGui.Button( "Clean Settings" ) )
            {
                Penumbra.CollectionManager.Current.CleanUnavailableSettings();
            }

            ImGuiCustom.HoverTooltip(
                "Remove all stored settings for mods not currently available and fix invalid settings.\nUse at own risk." );
        }

        private void DrawNewCollectionInput()
        {
            ImGui.SetNextItemWidth( SettingsMenu.InputTextWidth );
            ImGui.InputTextWithHint( "##New Collection", "New Collection Name", ref _newCollectionName, 64 );
            ImGui.SameLine();
            ImGuiComponents.HelpMarker(
                "A collection is a set of settings for your installed mods, including their enabled status, their priorities and their mod-specific configuration.\n"
              + "You can use multiple collections to quickly switch between sets of mods." );

            using var style = ImGuiRaii.PushStyle( ImGuiStyleVar.Alpha, 0.5f, _newCollectionName.Length == 0 );

            if( ImGui.Button( "Create New Empty Collection" ) && _newCollectionName.Length > 0 )
            {
                CreateNewCollection( false );
            }

            var hover = ImGui.IsItemHovered();
            ImGui.SameLine();
            if( ImGui.Button( "Duplicate Current Collection" ) && _newCollectionName.Length > 0 )
            {
                CreateNewCollection( true );
            }

            hover |= ImGui.IsItemHovered();

            style.Pop();
            if( _newCollectionName.Length == 0 && hover )
            {
                ImGui.SetTooltip( "Please enter a name before creating a collection." );
            }

            var deleteCondition = Penumbra.CollectionManager.Current.Name != ModCollection.DefaultCollection;
            ImGui.SameLine();
            if( ImGuiCustom.DisableButton( "Delete Current Collection", deleteCondition ) )
            {
                Penumbra.CollectionManager.RemoveCollection( Penumbra.CollectionManager.Current );
                SetCurrentCollection( Penumbra.CollectionManager.Current, true );
                UpdateNames();
            }

            if( !deleteCondition )
            {
                ImGuiCustom.HoverTooltip( "You can not delete the default collection." );
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

            Penumbra.CollectionManager.SetCollection( _collections[ idx + 1 ], ModCollection.Type.Current );
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
            ImGui.SetNextItemWidth( SettingsMenu.InputTextWidth );
            var combo = ImGui.Combo( "Current Collection", ref index, _collectionNames );
            ImGui.SameLine();
            ImGuiComponents.HelpMarker(
                "This collection will be modified when using the Installed Mods tab and making changes. It does not apply to anything by itself." );

            if( combo )
            {
                SetCurrentCollection( index, false );
            }
        }

        private static void DrawInheritance( ModCollection collection )
        {
            ImGui.PushID( collection.Index );
            if( ImGui.TreeNodeEx( collection.Name, ImGuiTreeNodeFlags.DefaultOpen ) )
            {
                foreach( var inheritance in collection.Inheritance )
                {
                    DrawInheritance( inheritance );
                }
            }

            ImGui.PopID();
        }

        private void DrawCurrentCollectionInheritance()
        {
            if( !ImGui.BeginListBox( "##inheritanceList",
                   new Vector2( SettingsMenu.InputTextWidth, ImGui.GetTextLineHeightWithSpacing() * 10 ) ) )
            {
                return;
            }

            using var end = ImGuiRaii.DeferredEnd( ImGui.EndListBox );
            DrawInheritance( _collections[ _currentCollectionIndex + 1 ] );
        }

        private static int _newInheritanceIdx = 0;

        private void DrawNewInheritanceSelection()
        {
            ImGui.SetNextItemWidth( SettingsMenu.InputTextWidth - ImGui.GetFrameHeight() - ImGui.GetStyle().ItemSpacing.X );
            if( ImGui.BeginCombo( "##newInheritance", Penumbra.CollectionManager[ _newInheritanceIdx ].Name ) )
            {
                using var end = ImGuiRaii.DeferredEnd( ImGui.EndCombo );
                foreach( var collection in Penumbra.CollectionManager )
                {
                    if( ImGui.Selectable( collection.Name, _newInheritanceIdx == collection.Index ) )
                    {
                        _newInheritanceIdx = collection.Index;
                    }
                }
            }

            ImGui.SameLine();
            var valid = _newInheritanceIdx                        > ModCollection.Empty.Index
             && _collections[ _currentCollectionIndex + 1 ].Index != _newInheritanceIdx
             && _collections[ _currentCollectionIndex + 1 ].Inheritance.All( c => c.Index != _newInheritanceIdx );
            using var style = ImGuiRaii.PushStyle( ImGuiStyleVar.Alpha, 0.5f, !valid );
            using var font  = ImGuiRaii.PushFont( UiBuilder.IconFont );
            if( ImGui.Button( $"{FontAwesomeIcon.Plus.ToIconString()}##newInheritanceAdd", ImGui.GetFrameHeight() * Vector2.One ) && valid )
            {
                _collections[ _currentCollectionIndex + 1 ].AddInheritance( Penumbra.CollectionManager[ _newInheritanceIdx ] );
            }

            style.Pop();
            font.Pop();
            ImGuiComponents.HelpMarker( "Add a new inheritance to the collection." );
        }

        private void DrawDefaultCollectionSelector()
        {
            var index = _currentDefaultIndex;
            ImGui.SetNextItemWidth( SettingsMenu.InputTextWidth );
            if( ImGui.Combo( "##Default Collection", ref index, _collectionNamesWithNone ) && index != _currentDefaultIndex )
            {
                Penumbra.CollectionManager.SetCollection( _collections[ index ], ModCollection.Type.Default );
                _currentDefaultIndex = index;
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker(
                "Mods in the default collection are loaded for any character that is not explicitly named in the character collections below.\n"
              + "They also take precedence before the forced collection." );

            ImGui.SameLine();
            ImGui.Text( "Default Collection" );
        }

        private void DrawNewCharacterCollection()
        {
            ImGui.SetNextItemWidth( SettingsMenu.InputTextWidth );
            ImGui.InputTextWithHint( "##New Character", "New Character Name", ref _newCharacterName, 32 );
            ImGui.SameLine();
            ImGuiComponents.HelpMarker( "Click Me for Information!" );
            ImGui.OpenPopupOnItemClick( CharacterCollectionHelpPopup, ImGuiPopupFlags.MouseButtonLeft );

            ImGui.SameLine();
            if( ImGuiCustom.DisableButton( "Create New Character Collection",
                   _newCharacterName.Length > 0 && Penumbra.Config.HasReadCharacterCollectionDesc ) )
            {
                Penumbra.CollectionManager.CreateCharacterCollection( _newCharacterName );
                _currentCharacterIndices[ _newCharacterName ] = 0;
                _newCharacterName                             = string.Empty;
            }

            ImGuiCustom.HoverTooltip( "Please enter a Character name before creating the collection.\n"
              + "You also need to have read the help text for character collections." );

            DrawCharacterCollectionHelp();
        }

        private static void DrawCharacterCollectionHelp()
        {
            var size = new Vector2( 700 * ImGuiHelpers.GlobalScale, 34 * ImGui.GetTextLineHeightWithSpacing() );
            ImGui.SetNextWindowPos( ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, Vector2.One / 2 );
            ImGui.SetNextWindowSize( size, ImGuiCond.Appearing );
            var _ = true;
            if( ImGui.BeginPopupModal( CharacterCollectionHelpPopup, ref _, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove ) )
            {
                const string header    = "Character Collections are a Hack! Use them at your own risk.";
                using var    end       = ImGuiRaii.DeferredEnd( ImGui.EndPopup );
                var          textWidth = ImGui.CalcTextSize( header ).X;
                ImGui.NewLine();
                ImGui.SetCursorPosX( ( size.X - textWidth ) / 2 );
                using var color = ImGuiRaii.PushColor( ImGuiCol.Text, 0xFF0000B8 );
                ImGui.Text( header );
                color.Pop();
                ImGui.NewLine();
                ImGui.TextWrapped(
                    "Character Collections are collections that get applied whenever the named character gets redrawn by Penumbra,"
                  + " whether by a manual '/penumbra redraw' command, or by the automatic redrawing feature.\n"
                  + "This means that they specifically require redrawing of a character to even apply, and thus can not work with mods that modify something that does not depend on characters being drawn, such as:\n"
                  + "        - animations\n"
                  + "        - sounds\n"
                  + "        - most effects\n"
                  + "        - most ui elements.\n"
                  + "They can also not work with actors that are not named, like the Character Preview or TryOn Actors, and they can not work in cutscenes, since redrawing in cutscenes would cancel all animations.\n"
                  + "They also do not work with every character customization (like skin, tattoo, hair, etc. changes) since those are not always re-requested by the game on redrawing a player. They may work, they may not, you need to test it.\n"
                  + "\n"
                  + "Due to the nature of meta manipulating mods, you can not mix meta manipulations inside a Character (or the Default) collection with meta manipulations inside the Forced collection.\n"
                  + "\n"
                  + "To verify that you have actually read this, you need to hold control and shift while clicking the Understood button for it to take effect.\n"
                  + "Due to the nature of redrawing being a hack, weird things (or maybe even crashes) may happen when using Character Collections. The way this works is:\n"
                  + "        - Penumbra queues a redraw of an actor.\n"
                  + "        - When the redraw queue reaches that actor, the actor gets undrawn (turned invisible).\n"
                  + "        - Penumbra checks the actors name and if it matches a Character Collection, it replaces the Default collection with that one.\n"
                  + "        - Penumbra triggers the redraw of that actor. The game requests files.\n"
                  + "        - Penumbra potentially redirects those file requests to the modded files in the active collection, which is either Default or Character. (Or, afterwards, Forced).\n"
                  + "        - The actor is drawn.\n"
                  + "        - Penumbra returns the active collection to the Default Collection.\n"
                  + "If any of those steps fails, or if the file requests take too long, it may happen that a character is drawn with half of its models from the Default and the other half from the Character Collection, or a modded Model is loaded, but not its corresponding modded textures, which lets it stay invisible, or similar problems." );

                var buttonSize = ImGuiHelpers.ScaledVector2( 150, 0 );
                var offset     = ( size.X - buttonSize.X ) / 2;
                ImGui.SetCursorPos( new Vector2( offset, size.Y - 3 * ImGui.GetTextLineHeightWithSpacing() ) );
                var state = ImGui.GetIO().KeyCtrl && ImGui.GetIO().KeyShift;
                color.Push( ImGuiCol.ButtonHovered, 0xFF00A000, state );
                if( ImGui.Button( "Understood!", buttonSize ) )
                {
                    if( state && !Penumbra.Config.HasReadCharacterCollectionDesc )
                    {
                        Penumbra.Config.HasReadCharacterCollectionDesc = true;
                        Penumbra.Config.Save();
                    }

                    ImGui.CloseCurrentPopup();
                }
            }
        }


        private void DrawCharacterCollectionSelectors()
        {
            using var raii = ImGuiRaii.DeferredEnd( ImGui.EndChild );
            if( !ImGui.BeginChild( "##CollectionChild", AutoFillSize, true ) )
            {
                return;
            }

            DrawDefaultCollectionSelector();

            foreach( var (name, collection) in Penumbra.CollectionManager.Characters.ToArray() )
            {
                var idx = _currentCharacterIndices[ name ];
                var tmp = idx;
                ImGui.SetNextItemWidth( SettingsMenu.InputTextWidth );
                if( ImGui.Combo( $"##{name}collection", ref tmp, _collectionNamesWithNone ) && idx != tmp )
                {
                    Penumbra.CollectionManager.SetCollection( _collections[ tmp ], ModCollection.Type.Character, name );
                    _currentCharacterIndices[ name ] = tmp;
                }

                ImGui.SameLine();

                using var font = ImGuiRaii.PushFont( UiBuilder.IconFont );

                if( ImGui.Button( $"{FontAwesomeIcon.Trash.ToIconString()}##{name}", Vector2.One * ImGui.GetFrameHeight() ) )
                {
                    Penumbra.CollectionManager.RemoveCharacterCollection( name );
                }

                font.Pop();

                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
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

            if( ImGui.BeginChild( "##CollectionHandling", new Vector2( -1, ImGui.GetTextLineHeightWithSpacing() * 17 ), true ) )
            {
                DrawCurrentCollectionSelector( true );
                ImGuiHelpers.ScaledDummy( 0, 10 );
                DrawNewCollectionInput();
                ImGuiHelpers.ScaledDummy( 0, 10 );
                DrawCurrentCollectionInheritance();
                DrawNewInheritanceSelection();
            }

            raii.Pop();

            DrawCharacterCollectionSelectors();
        }
    }
}