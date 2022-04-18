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
    private string _newCollectionName = string.Empty;
    private string _newCharacterName  = string.Empty;

    private void CreateNewCollection( bool duplicate )
    {
        if( Penumbra.CollectionManager.AddCollection( _newCollectionName, duplicate ? Penumbra.CollectionManager.Current : null ) )
        {
            _newCollectionName = string.Empty;
        }
    }

    private static void DrawCleanCollectionButton()
    {
        if( ImGui.Button( "Clean Settings" ) )
        {
            Penumbra.CollectionManager.Current.CleanUnavailableSettings();
        }

        ImGuiUtil.HoverTooltip( "Remove all stored settings for mods not currently available and fix invalid settings.\nUse at own risk." );
    }

    private void DrawNewCollectionInput()
    {
        ImGui.SetNextItemWidth( _inputTextWidth.X );
        ImGui.InputTextWithHint( "##New Collection", "New Collection Name", ref _newCollectionName, 64 );
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(
            "A collection is a set of settings for your installed mods, including their enabled status, their priorities and their mod-specific configuration.\n"
          + "You can use multiple collections to quickly switch between sets of mods." );

        var createCondition = _newCollectionName.Length > 0;
        var tt              = createCondition ? string.Empty : "Please enter a name before creating a collection.";
        if( ImGuiUtil.DrawDisabledButton( "Create New Empty Collection", Vector2.Zero, tt, !createCondition ) )
        {
            CreateNewCollection( false );
        }

        ImGui.SameLine();
        if( ImGuiUtil.DrawDisabledButton( "Duplicate Current Collection", Vector2.Zero, tt, !createCondition ) )
        {
            CreateNewCollection( true );
        }

        var deleteCondition = Penumbra.CollectionManager.Current.Name != ModCollection.DefaultCollection;
        tt = deleteCondition ? string.Empty : "You can not delete the default collection.";
        ImGui.SameLine();
        if( ImGuiUtil.DrawDisabledButton( "Delete Current Collection", Vector2.Zero, tt, !deleteCondition ) )
        {
            Penumbra.CollectionManager.RemoveCollection( Penumbra.CollectionManager.Current );
        }

        if( Penumbra.Config.ShowAdvanced )
        {
            ImGui.SameLine();
            DrawCleanCollectionButton();
        }
    }

    public void DrawCurrentCollectionSelector()
    {
        DrawCollectionSelector( "##current", _inputTextWidth.X, ModCollection.Type.Current, false, null );
        ImGui.SameLine();
        ImGuiUtil.LabeledHelpMarker( "Current Collection",
            "This collection will be modified when using the Installed Mods tab and making changes. It does not apply to anything by itself." );
    }

    private void DrawDefaultCollectionSelector()
    {
        DrawCollectionSelector( "##default", _inputTextWidth.X, ModCollection.Type.Default, true, null );
        ImGui.SameLine();
        ImGuiUtil.LabeledHelpMarker( "Default Collection",
            "Mods in the default collection are loaded for any character that is not explicitly named in the character collections below.\n"
          + "They also take precedence before the forced collection." );
    }

    private void DrawNewCharacterCollection()
    {
        const string description = "Character Collections apply specifically to game objects of the given name.\n"
          + "The default collection does not apply to any character that has a character collection specified.\n"
          + "Certain actors - like the ones in cutscenes or preview windows - will try to use appropriate character collections.\n";

        ImGui.SetNextItemWidth(_inputTextWidth.X );
        ImGui.InputTextWithHint( "##NewCharacter", "New Character Name", ref _newCharacterName, 32 );
        ImGui.SameLine();
        var disabled = _newCharacterName.Length == 0;
        var tt       = disabled ? "Please enter a Character name before creating the collection.\n\n" + description : description;
        if( ImGuiUtil.DrawDisabledButton( "Create New Character Collection", Vector2.Zero, tt, disabled) )
        {
            Penumbra.CollectionManager.CreateCharacterCollection( _newCharacterName );
            _newCharacterName                           = string.Empty;
        }
    }

    private void DrawCharacterCollectionSelectors()
    {
        using var child = ImRaii.Child( "##Collections", -Vector2.One, true );
        if( !child )
            return;

        DrawDefaultCollectionSelector();

        foreach( var name in Penumbra.CollectionManager.Characters.Keys.ToArray() )
        {
            using var id = ImRaii.PushId( name );
            DrawCollectionSelector( string.Empty, _inputTextWidth.X, ModCollection.Type.Character, true, name );
            ImGui.SameLine();
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Trash.ToIconString(), Vector2.One * ImGui.GetFrameHeight(), string.Empty, false, true) )
            {
                Penumbra.CollectionManager.RemoveCharacterCollection( name );
            }
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.Text( name );
        }
        DrawNewCharacterCollection();
    }

    //private static void DrawInheritance( ModCollection collection )
    //    {
    //        ImGui.PushID( collection.Index );
    //        if( ImGui.TreeNodeEx( collection.Name, ImGuiTreeNodeFlags.DefaultOpen ) )
    //        {
    //            foreach( var inheritance in collection.Inheritance )
    //            {
    //                DrawInheritance( inheritance );
    //            }
    //        }
    //
    //        ImGui.PopID();
    //    }
    //
    //    private void DrawCurrentCollectionInheritance()
    //    {
    //        if( !ImGui.BeginListBox( "##inheritanceList",
    //               new Vector2( SettingsMenu.InputTextWidth, ImGui.GetTextLineHeightWithSpacing() * 10 ) ) )
    //        {
    //            return;
    //        }
    //
    //        using var end = ImGuiRaii.DeferredEnd( ImGui.EndListBox );
    //        DrawInheritance( _collections[ _currentCollectionIndex + 1 ] );
    //    }
    //
    //private static int _newInheritanceIdx = 0;
    //
    //private void DrawNewInheritanceSelection()
    //{
    //    ImGui.SetNextItemWidth( SettingsMenu.InputTextWidth - ImGui.GetFrameHeight() - ImGui.GetStyle().ItemSpacing.X );
    //    if( ImGui.BeginCombo( "##newInheritance", Penumbra.CollectionManager[ _newInheritanceIdx ].Name ) )
    //    {
    //        using var end = ImGuiRaii.DeferredEnd( ImGui.EndCombo );
    //        foreach( var collection in Penumbra.CollectionManager )
    //        {
    //            if( ImGui.Selectable( collection.Name, _newInheritanceIdx == collection.Index ) )
    //            {
    //                _newInheritanceIdx = collection.Index;
    //            }
    //        }
    //    }
    //
    //    ImGui.SameLine();
    //    var valid = _newInheritanceIdx                        > ModCollection.Empty.Index
    //     && _collections[ _currentCollectionIndex + 1 ].Index != _newInheritanceIdx
    //     && _collections[ _currentCollectionIndex + 1 ].Inheritance.All( c => c.Index != _newInheritanceIdx );
    //    using var style = ImGuiRaii.PushStyle( ImGuiStyleVar.Alpha, 0.5f, !valid );
    //    using var font  = ImGuiRaii.PushFont( UiBuilder.IconFont );
    //    if( ImGui.Button( $"{FontAwesomeIcon.Plus.ToIconString()}##newInheritanceAdd", ImGui.GetFrameHeight() * Vector2.One ) && valid )
    //    {
    //        _collections[ _currentCollectionIndex + 1 ].AddInheritance( Penumbra.CollectionManager[ _newInheritanceIdx ] );
    //    }
    //
    //    style.Pop();
    //    font.Pop();
    //    ImGuiComponents.HelpMarker( "Add a new inheritance to the collection." );
    //}

    private void DrawMainSelectors()
    {
        using var main = ImRaii.Child( "##CollectionsMain", new Vector2( -1, ImGui.GetTextLineHeightWithSpacing() * 17 ), true );
        if( !main )
        {
            return;
        }

        DrawCurrentCollectionSelector();
        ImGuiHelpers.ScaledDummy( 0, 10 );
        DrawNewCollectionInput();
    }

    public void DrawCollectionsTab()
    {
        using var tab = ImRaii.TabItem( "Collections" );
        if( !tab )
        {
            return;
        }

        
        DrawMainSelectors();
        DrawCharacterCollectionSelectors();
    }

}