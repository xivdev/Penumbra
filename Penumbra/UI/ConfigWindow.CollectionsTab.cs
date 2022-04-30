using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Collections;

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

            DrawMainSelectors();
            DrawCharacterCollectionSelectors();
        }


        // Input text fields.
        private string _newCollectionName = string.Empty;
        private bool   _canAddCollection  = false;
        private string _newCharacterName  = string.Empty;

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
            DrawCollectionSelector( "##current", _window._inputTextWidth.X, ModCollection.Type.Current, false, null );
            ImGui.SameLine();
            ImGuiUtil.LabeledHelpMarker( "Current Collection",
                "This collection will be modified when using the Installed Mods tab and making changes. It does not apply to anything by itself." );
        }

        private void DrawDefaultCollectionSelector()
        {
            DrawCollectionSelector( "##default", _window._inputTextWidth.X, ModCollection.Type.Default, true, null );
            ImGui.SameLine();
            ImGuiUtil.LabeledHelpMarker( "Default Collection",
                "Mods in the default collection are loaded for any character that is not explicitly named in the character collections below.\n"
              + "They also take precedence before the forced collection." );
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
            using var child = ImRaii.Child( "##Collections", -Vector2.One, true );
            if( !child )
            {
                return;
            }

            DrawDefaultCollectionSelector();

            foreach( var name in Penumbra.CollectionManager.Characters.Keys.ToArray() )
            {
                using var id = ImRaii.PushId( name );
                DrawCollectionSelector( string.Empty, _window._inputTextWidth.X, ModCollection.Type.Character, true, name );
                ImGui.SameLine();
                if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Trash.ToIconString(), _window._iconButtonSize, string.Empty,
                       false,
                       true ) )
                {
                    Penumbra.CollectionManager.RemoveCharacterCollection( name );
                }

                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted( name );
            }

            DrawNewCharacterCollection();
        }

        private void DrawMainSelectors()
        {
            var size = new Vector2( -1,
                ImGui.GetTextLineHeightWithSpacing() * InheritedCollectionHeight
              + _window._defaultSpace.Y              * 2
              + ImGui.GetFrameHeightWithSpacing()    * 4
              + ImGui.GetStyle().ItemSpacing.Y       * 6 );
            using var main = ImRaii.Child( "##CollectionsMain", size, true );
            if( !main )
            {
                return;
            }

            DrawCurrentCollectionSelector();
            ImGui.Dummy( _window._defaultSpace );
            DrawNewCollectionInput();
            ImGui.Dummy( _window._defaultSpace );
            DrawInheritanceBlock();
        }
    }
}