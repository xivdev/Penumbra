using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Collections;
using Penumbra.Util;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    // Encapsulate for less pollution.
    private partial class CollectionsTab
    {
        private readonly ConfigWindow _window;

        public CollectionsTab( ConfigWindow window )
            => _window = window;

        public void Draw()
        {
            using var tab = ImRaii.TabItem( "Collections" );
            if( !tab )
            {
                return;
            }

            DrawCharacterCollectionSelectors();
            DrawMainSelectors();
        }


        // Input text fields.
        private string          _newCollectionName = string.Empty;
        private bool            _canAddCollection  = false;
        private string          _newCharacterName  = string.Empty;
        private CollectionType? _currentType       = CollectionType.Yourself;

        // Create a new collection that is either empty or a duplicate of the current collection.
        // Resets the new collection name.
        private void CreateNewCollection( bool duplicate )
        {
            if( Penumbra.CollectionManager.AddCollection( _newCollectionName, duplicate ? Penumbra.CollectionManager.Current : null ) )
            {
                _newCollectionName = string.Empty;
            }
        }

        // Only gets drawn when actually relevant.
        private static void DrawCleanCollectionButton()
        {
            if( Penumbra.Config.ShowAdvanced && Penumbra.CollectionManager.Current.HasUnusedSettings )
            {
                ImGui.SameLine();
                if( ImGuiUtil.DrawDisabledButton( "Clean Settings", Vector2.Zero
                       , "Remove all stored settings for mods not currently available and fix invalid settings.\nUse at own risk."
                       , false ) )
                {
                    Penumbra.CollectionManager.Current.CleanUnavailableSettings();
                }
            }
        }

        // Draw the new collection input as well as its buttons.
        private void DrawNewCollectionInput()
        {
            // Input for new collection name. Also checks for validity when changed.
            ImGui.SetNextItemWidth( _window._inputTextWidth.X );
            if( ImGui.InputTextWithHint( "##New Collection", "New Collection Name", ref _newCollectionName, 64 ) )
            {
                _canAddCollection = Penumbra.CollectionManager.CanAddCollection( _newCollectionName, out _ );
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker(
                "A collection is a set of settings for your installed mods, including their enabled status, their priorities and their mod-specific configuration.\n"
              + "You can use multiple collections to quickly switch between sets of mods." );

            // Creation buttons.
            var tt = _canAddCollection ? string.Empty : "Please enter a unique name before creating a collection.";
            if( ImGuiUtil.DrawDisabledButton( "Create New Empty Collection", Vector2.Zero, tt, !_canAddCollection ) )
            {
                CreateNewCollection( false );
            }

            ImGui.SameLine();
            if( ImGuiUtil.DrawDisabledButton( "Duplicate Current Collection", Vector2.Zero, tt, !_canAddCollection ) )
            {
                CreateNewCollection( true );
            }

            // Deletion conditions.
            var deleteCondition = Penumbra.CollectionManager.Current.Name != ModCollection.DefaultCollection;
            tt = deleteCondition ? string.Empty : "You can not delete the default collection.";
            ImGui.SameLine();
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Trash.ToIconString(), Vector2.Zero, tt, !deleteCondition, true ) )
            {
                Penumbra.CollectionManager.RemoveCollection( Penumbra.CollectionManager.Current );
            }

            DrawCleanCollectionButton();
        }

        private void DrawCurrentCollectionSelector()
        {
            DrawCollectionSelector( "##current", _window._inputTextWidth.X, CollectionType.Current, false, null );
            ImGui.SameLine();
            ImGuiUtil.LabeledHelpMarker( "Current Collection",
                "This collection will be modified when using the Installed Mods tab and making changes. It does not apply to anything by itself." );
        }

        private void DrawDefaultCollectionSelector()
        {
            DrawCollectionSelector( "##default", _window._inputTextWidth.X, CollectionType.Default, true, null );
            ImGui.SameLine();
            ImGuiUtil.LabeledHelpMarker( "Default Collection",
                "Mods in the default collection are loaded for any character that is not explicitly named in the character collections below.\n" );
        }

        // We do not check for valid character names.
        private void DrawNewSpecialCollection()
        {
            const string description = "Special Collections apply to certain types of characters.\n"
              + "All of them take precedence before the Default collection,\n"
              + "but all character collections take precedence before them.";

            ImGui.SetNextItemWidth( _window._inputTextWidth.X );
            if( _currentType == null || Penumbra.CollectionManager.ByType( _currentType.Value ) != null )
            {
                _currentType = CollectionTypeExtensions.Special.FindFirst( t => Penumbra.CollectionManager.ByType( t ) == null, out var t2 )
                    ? t2
                    : null;
            }

            if( _currentType == null )
            {
                return;
            }

            using( var combo = ImRaii.Combo( "##NewSpecial", _currentType.Value.ToName() ) )
            {
                if( combo )
                {
                    foreach( var type in CollectionTypeExtensions.Special.Where( t => Penumbra.CollectionManager.ByType( t ) == null ) )
                    {
                        if( ImGui.Selectable( type.ToName(), type == _currentType.Value ) )
                        {
                            _currentType = type;
                        }
                    }
                }
            }

            ImGui.SameLine();
            var disabled = _currentType == null;
            var tt       = disabled ? "Please select a special collection type before creating the collection.\n\n" + description : description;
            if( ImGuiUtil.DrawDisabledButton( "Create New Special Collection", Vector2.Zero, tt, disabled ) )
            {
                Penumbra.CollectionManager.CreateSpecialCollection( _currentType!.Value );
                _currentType = null;
            }
        }

        // We do not check for valid character names.
        private void DrawNewCharacterCollection()
        {
            const string description = "Character Collections apply specifically to game objects of the given name.\n"
              + "The default collection does not apply to any character that has a character collection specified.\n"
              + "Certain actors - like the ones in cutscenes or preview windows - will try to use appropriate character collections.\n";

            ImGui.SetNextItemWidth( _window._inputTextWidth.X );
            ImGui.InputTextWithHint( "##NewCharacter", "New Character Name", ref _newCharacterName, 32 );
            ImGui.SameLine();
            var disabled = _newCharacterName.Length == 0;
            var tt       = disabled ? "Please enter a Character name before creating the collection.\n\n" + description : description;
            if( ImGuiUtil.DrawDisabledButton( "Create New Character Collection", Vector2.Zero, tt, disabled ) )
            {
                Penumbra.CollectionManager.CreateCharacterCollection( _newCharacterName );
                _newCharacterName = string.Empty;
            }
        }

        private void DrawCharacterCollectionSelectors()
        {
            ImGui.Dummy( _window._defaultSpace );
            if( ImGui.CollapsingHeader( "Active Collections", ImGuiTreeNodeFlags.DefaultOpen ) )
            {
                ImGui.Dummy( _window._defaultSpace );
                DrawDefaultCollectionSelector();
                ImGui.Dummy( _window._defaultSpace );
                foreach( var type in CollectionTypeExtensions.Special )
                {
                    var collection = Penumbra.CollectionManager.ByType( type );
                    if( collection != null )
                    {
                        using var id = ImRaii.PushId( ( int )type );
                        DrawCollectionSelector( string.Empty, _window._inputTextWidth.X, type, true, null );
                        ImGui.SameLine();
                        if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Trash.ToIconString(), _window._iconButtonSize, string.Empty,
                               false, true ) )
                        {
                            Penumbra.CollectionManager.RemoveSpecialCollection( type );
                        }

                        ImGui.SameLine();
                        ImGui.AlignTextToFramePadding();
                        ImGuiUtil.LabeledHelpMarker( type.ToName(), type.ToDescription() );
                    }
                }

                DrawNewSpecialCollection();
                ImGui.Dummy( _window._defaultSpace );

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

                DrawNewCharacterCollection();
                ImGui.Dummy( _window._defaultSpace );
            }
        }

        private void DrawMainSelectors()
        {
            ImGui.Dummy( _window._defaultSpace );
            if( ImGui.CollapsingHeader( "Collection Settings", ImGuiTreeNodeFlags.DefaultOpen ) )
            {
                ImGui.Dummy( _window._defaultSpace );
                DrawCurrentCollectionSelector();
                ImGui.Dummy( _window._defaultSpace );
                DrawNewCollectionInput();
                ImGui.Dummy( _window._defaultSpace );
                DrawInheritanceBlock();
            }
        }
    }
}