using System;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Collections;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    // Encapsulate for less pollution.
    private partial class CollectionsTab : IDisposable
    {
        private readonly ConfigWindow _window;

        public CollectionsTab( ConfigWindow window )
        {
            _window = window;

            Penumbra.CollectionManager.CollectionChanged += UpdateIdentifiers;
        }

        public void Dispose()
            => Penumbra.CollectionManager.CollectionChanged -= UpdateIdentifiers;

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
        private string _newCollectionName = string.Empty;
        private bool   _canAddCollection;

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
            DrawCollectionSelector( "##current", _window._inputTextWidth.X, CollectionType.Current, false );
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
            DrawCollectionSelector( "##default", _window._inputTextWidth.X, CollectionType.Default, true );
            ImGui.SameLine();
            ImGuiUtil.LabeledHelpMarker( DefaultCollection,
                $"Mods in the {DefaultCollection} are loaded for anything that is not associated with the user interface or a character in the game,"
              + "as well as any character for whom no more specific conditions from below apply." );
        }

        private void DrawInterfaceCollectionSelector()
        {
            using var group = ImRaii.Group();
            DrawCollectionSelector( "##interface", _window._inputTextWidth.X, CollectionType.Interface, true );
            ImGui.SameLine();
            ImGuiUtil.LabeledHelpMarker( InterfaceCollection,
                $"Mods in the {InterfaceCollection} are loaded for any file that the game categorizes as an UI file. This is mostly icons as well as the tiles that generate the user interface windows themselves." );
        }

        private sealed class SpecialCombo : FilterComboBase< (CollectionType, string, string) >
        {
            public (CollectionType, string, string)? CurrentType
                => CollectionTypeExtensions.Special[ CurrentIdx ];

            public           int    CurrentIdx;
            private readonly float  _unscaledWidth;
            private readonly string _label;

            public SpecialCombo( string label, float unscaledWidth )
                : base( CollectionTypeExtensions.Special, false )
            {
                _label         = label;
                _unscaledWidth = unscaledWidth;
            }

            public void Draw()
            {
                var preview = CurrentIdx >= 0 ? Items[ CurrentIdx ].Item2 : string.Empty;
                Draw( _label, preview, ref CurrentIdx, _unscaledWidth * ImGuiHelpers.GlobalScale, ImGui.GetTextLineHeightWithSpacing() );
            }

            protected override string ToString( (CollectionType, string, string) obj )
                => obj.Item2;

            protected override bool IsVisible( int globalIdx, LowerString filter )
            {
                var obj = Items[ globalIdx ];
                return filter.IsContained( obj.Item2 ) && Penumbra.CollectionManager.ByType( obj.Item1 ) == null;
            }
        }

        private readonly SpecialCombo _specialCollectionCombo = new("##NewSpecial", 350);

        private const string CharacterGroupDescription = $"{CharacterGroups} apply to certain types of characters based on a condition.\n"
          + $"All of them take precedence before the {DefaultCollection},\n"
          + $"but all {IndividualAssignments} take precedence before them.";


        // We do not check for valid character names.
        private void DrawNewSpecialCollection()
        {
            ImGui.SetNextItemWidth( _window._inputTextWidth.X );
            if( _specialCollectionCombo.CurrentIdx                                                   == -1
            || Penumbra.CollectionManager.ByType( _specialCollectionCombo.CurrentType!.Value.Item1 ) != null )
            {
                _specialCollectionCombo.ResetFilter();
                _specialCollectionCombo.CurrentIdx = CollectionTypeExtensions.Special
                   .IndexOf( t => Penumbra.CollectionManager.ByType( t.Item1 ) == null );
            }

            if( _specialCollectionCombo.CurrentType == null )
            {
                return;
            }

            _specialCollectionCombo.Draw();
            ImGui.SameLine();
            var disabled = _specialCollectionCombo.CurrentType == null;
            var tt = disabled
                ? $"Please select a condition for a {GroupAssignment} before creating the collection.\n\n" + CharacterGroupDescription
                : CharacterGroupDescription;
            if( ImGuiUtil.DrawDisabledButton( $"Assign {ConditionalGroup}", new Vector2( 120 * ImGuiHelpers.GlobalScale, 0 ), tt, disabled ) )
            {
                Penumbra.CollectionManager.CreateSpecialCollection( _specialCollectionCombo.CurrentType!.Value.Item1 );
                _specialCollectionCombo.CurrentIdx = -1;
            }
        }

        private void DrawSpecialCollections()
        {
            foreach( var (type, name, desc) in CollectionTypeExtensions.Special )
            {
                var collection = Penumbra.CollectionManager.ByType( type );
                if( collection != null )
                {
                    using var id = ImRaii.PushId( ( int )type );
                    DrawCollectionSelector( string.Empty, _window._inputTextWidth.X, type, true );
                    ImGui.SameLine();
                    if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Trash.ToIconString(), _window._iconButtonSize, string.Empty,
                           false, true ) )
                    {
                        Penumbra.CollectionManager.RemoveSpecialCollection( type );
                        _specialCollectionCombo.ResetFilter();
                    }

                    ImGui.SameLine();
                    ImGui.AlignTextToFramePadding();
                    ImGuiUtil.LabeledHelpMarker( name, desc );
                }
            }
        }

        private void DrawSpecialAssignments()
        {
            using var _ = ImRaii.Group();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted( CharacterGroups );
            ImGuiComponents.HelpMarker( CharacterGroupDescription );
            ImGui.Separator();
            DrawSpecialCollections();
            ImGui.Dummy( Vector2.Zero );
            DrawNewSpecialCollection();
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
            DrawInterfaceCollectionSelector();
            OpenTutorial( BasicTutorialSteps.InterfaceCollection );
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