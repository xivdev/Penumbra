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
            OpenTutorial( BasicTutorialSteps.Collections );
            if( !tab )
            {
                return;
            }

            using var child = ImRaii.Child( "##collections", -Vector2.One );
            if( child )
            {
                DrawActiveCollectionSelectors();
                DrawMainSelectors();
            }
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
        private static void DrawCleanCollectionButton( Vector2 width )
        {
            if( Penumbra.CollectionManager.Current.HasUnusedSettings )
            {
                ImGui.SameLine();
                if( ImGuiUtil.DrawDisabledButton(
                       $"Clean {Penumbra.CollectionManager.Current.NumUnusedSettings} Unused Settings###CleanSettings", width
                       , "Remove all stored settings for mods not currently available and fix invalid settings.\n\nUse at own risk."
                       , false ) )
                {
                    Penumbra.CollectionManager.Current.CleanUnavailableSettings();
                }
            }
        }

        // Draw the new collection input as well as its buttons.
        private void DrawNewCollectionInput( Vector2 width )
        {
            // Input for new collection name. Also checks for validity when changed.
            ImGui.SetNextItemWidth( _window._inputTextWidth.X );
            if( ImGui.InputTextWithHint( "##New Collection", "New Collection Name...", ref _newCollectionName, 64 ) )
            {
                _canAddCollection = Penumbra.CollectionManager.CanAddCollection( _newCollectionName, out _ );
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker(
                "A collection is a set of settings for your installed mods, including their enabled status, their priorities and their mod-specific configuration.\n"
              + "You can use multiple collections to quickly switch between sets of enabled mods." );

            // Creation buttons.
            var tt = _canAddCollection
                ? string.Empty
                : "Please enter a unique name only consisting of symbols valid in a path but no '|' before creating a collection.";
            if( ImGuiUtil.DrawDisabledButton( "Create Empty Collection", width, tt, !_canAddCollection ) )
            {
                CreateNewCollection( false );
            }

            ImGui.SameLine();
            if( ImGuiUtil.DrawDisabledButton( $"Duplicate {SelectedCollection}", width, tt, !_canAddCollection ) )
            {
                CreateNewCollection( true );
            }
        }

        private void DrawCurrentCollectionSelector( Vector2 width )
        {
            using var group = ImRaii.Group();
            DrawCollectionSelector( "##current", _window._inputTextWidth.X, CollectionType.Current, false, null );
            ImGui.SameLine();
            ImGuiUtil.LabeledHelpMarker( SelectedCollection,
                "This collection will be modified when using the Installed Mods tab and making changes.\nIt is not automatically assigned to anything." );

            // Deletion conditions.
            var deleteCondition = Penumbra.CollectionManager.Current.Name != ModCollection.DefaultCollection;
            var modifierHeld    = Penumbra.Config.DeleteModModifier.IsActive();
            var tt = deleteCondition
                ? modifierHeld ? string.Empty : $"Hold {Penumbra.Config.DeleteModModifier} while clicking to delete the collection."
                : $"You can not delete the collection {ModCollection.DefaultCollection}.";

            if( ImGuiUtil.DrawDisabledButton( $"Delete {SelectedCollection}", width, tt, !deleteCondition || !modifierHeld ) )
            {
                Penumbra.CollectionManager.RemoveCollection( Penumbra.CollectionManager.Current );
            }

            DrawCleanCollectionButton( width );
        }

        private void DrawDefaultCollectionSelector()
        {
            using var group = ImRaii.Group();
            DrawCollectionSelector( "##default", _window._inputTextWidth.X, CollectionType.Default, true, null );
            ImGui.SameLine();
            ImGuiUtil.LabeledHelpMarker( DefaultCollection,
                $"Mods in the {DefaultCollection} are loaded for anything that is not associated with a character in the game "
              + "as well as any character for whom no more specific conditions from below apply." );
        }

        // We do not check for valid character names.
        private void DrawNewSpecialCollection()
        {
            const string description = $"{CharacterGroups} apply to certain types of characters based on a condition.\n"
              + $"All of them take precedence before the {DefaultCollection},\n"
              + $"but all {IndividualAssignments} take precedence before them.";

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
            var tt = disabled
                ? $"Please select a condition for a {GroupAssignment} before creating the collection.\n\n" + description
                : description;
            if( ImGuiUtil.DrawDisabledButton( $"Assign {ConditionalGroup}", new Vector2( 120 * ImGuiHelpers.GlobalScale, 0 ), tt, disabled ) )
            {
                Penumbra.CollectionManager.CreateSpecialCollection( _currentType!.Value );
                _currentType = null;
            }
        }

        // We do not check for valid character names.
        private void DrawNewCharacterCollection()
        {
            const string description = "Character Collections apply specifically to individual game objects of the given name.\n"
              + $"More general {GroupAssignment} or the {DefaultCollection} do not apply if an .\n"
              + "Certain actors - like the ones in cutscenes or preview windows - will try to use appropriate character collections.\n";

            ImGui.SetNextItemWidth( _window._inputTextWidth.X );
            ImGui.InputTextWithHint( "##NewCharacter", "Character Name...", ref _newCharacterName, 32 );
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
        }

        private void DrawSpecialCollections()
        {
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
        }

        private void DrawSpecialAssignments()
        {
            using var _ = ImRaii.Group();
            ImGui.TextUnformatted( CharacterGroups );
            ImGui.Separator();
            DrawSpecialCollections();
            ImGui.Dummy( Vector2.Zero );
            DrawNewSpecialCollection();
        }

        private void DrawIndividualAssignments()
        {
            using var _ = ImRaii.Group();
            ImGui.TextUnformatted( $"Individual {ConditionalIndividual}s"  );
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

        private void DrawActiveCollectionSelectors()
        {
            ImGui.Dummy( _window._defaultSpace );
            var open = ImGui.CollapsingHeader( ActiveCollections, ImGuiTreeNodeFlags.DefaultOpen );
            OpenTutorial( BasicTutorialSteps.ActiveCollections );
            if( !open )
            {
                return;
            }

            ImGui.Dummy( _window._defaultSpace );
            DrawDefaultCollectionSelector();
            OpenTutorial( BasicTutorialSteps.DefaultCollection );
            ImGui.Dummy( _window._defaultSpace );

            DrawSpecialAssignments();
            OpenTutorial( BasicTutorialSteps.SpecialCollections1 );

            ImGui.Dummy( _window._defaultSpace );

            DrawIndividualAssignments();
            OpenTutorial( BasicTutorialSteps.SpecialCollections2 );

            ImGui.Dummy( _window._defaultSpace );
        }

        private void DrawMainSelectors()
        {
            ImGui.Dummy( _window._defaultSpace );
            var open = ImGui.CollapsingHeader( "Collection Settings", ImGuiTreeNodeFlags.DefaultOpen );
            OpenTutorial( BasicTutorialSteps.EditingCollections );
            if( !open )
            {
                return;
            }

            var width = new Vector2( ( _window._inputTextWidth.X - ImGui.GetStyle().ItemSpacing.X ) / 2, 0 );
            ImGui.Dummy( _window._defaultSpace );
            DrawCurrentCollectionSelector( width );
            OpenTutorial( BasicTutorialSteps.CurrentCollection );
            ImGui.Dummy( _window._defaultSpace );
            DrawNewCollectionInput( width );
            ImGui.Dummy( _window._defaultSpace );
            DrawInheritanceBlock();
            OpenTutorial( BasicTutorialSteps.Inheritance );
        }
    }
}