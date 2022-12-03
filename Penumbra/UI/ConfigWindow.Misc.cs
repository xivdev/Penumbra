using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using ImGuiNET;
using Lumina.Data.Parsing;
using Lumina.Excel.GeneratedSheets;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    // Draw text given by a ByteString.
    internal static unsafe void Text( ByteString s )
        => ImGuiNative.igTextUnformatted( s.Path, s.Path + s.Length );

    // Draw text given by a byte pointer.
    private static unsafe void Text( byte* s, int length )
        => ImGuiNative.igTextUnformatted( s, s + length );

    // Draw the name of a resource file.
    private static unsafe void Text( ResourceHandle* resource )
        => Text( resource->FileName(), resource->FileNameLength );

    // Draw a ByteString as a selectable.
    internal static unsafe bool Selectable( ByteString s, bool selected )
    {
        var tmp = ( byte )( selected ? 1 : 0 );
        return ImGuiNative.igSelectable_Bool( s.Path, tmp, ImGuiSelectableFlags.None, Vector2.Zero ) != 0;
    }

    // Apply Changed Item Counters to the Name if necessary.
    private static string ChangedItemName( string name, object? data )
        => data is int counter ? $"{counter} Files Manipulating {name}s" : name;

    // Draw a changed item, invoking the Api-Events for clicks and tooltips.
    // Also draw the item Id in grey if requested
    private void DrawChangedItem( string name, object? data, bool drawId )
    {
        name = ChangedItemName( name, data );
        var ret = ImGui.Selectable( name ) ? MouseButton.Left : MouseButton.None;
        ret = ImGui.IsItemClicked( ImGuiMouseButton.Right ) ? MouseButton.Right : ret;
        ret = ImGui.IsItemClicked( ImGuiMouseButton.Middle ) ? MouseButton.Middle : ret;

        if( ret != MouseButton.None )
        {
            _penumbra.Api.InvokeClick( ret, data );
        }

        if( _penumbra.Api.HasTooltip && ImGui.IsItemHovered() )
        {
            // We can not be sure that any subscriber actually prints something in any case.
            // Circumvent ugly blank tooltip with less-ugly useless tooltip.
            using var tt    = ImRaii.Tooltip();
            using var group = ImRaii.Group();
            _penumbra.Api.InvokeTooltip( data );
            group.Dispose();
            if( ImGui.GetItemRectSize() == Vector2.Zero )
            {
                ImGui.TextUnformatted( "No actions available." );
            }
        }

        if( drawId && DrawChangedItemObject( data, out var text ) )
        {
            ImGui.SameLine( ImGui.GetContentRegionAvail().X );
            ImGuiUtil.RightJustify( text, ColorId.ItemId.Value() );
        }
    }

    private static bool DrawChangedItemObject( object? obj, out string text )
    {
        switch( obj )
        {
            case Item it:
                var quad = ( Quad )it.ModelMain;
                text = quad.C == 0 ? $"({quad.A}-{quad.B})" : $"({quad.A}-{quad.B}-{quad.C})";
                return true;
            case ModelChara m:
                text = $"({( ( CharacterBase.ModelType )m.Type ).ToName()} {m.Model}-{m.Base}-{m.Variant})";
                return true;
            default:
                text = string.Empty;
                return false;
        }
    }

    // A selectable that copies its text to clipboard on selection and provides a on-hover tooltip about that,
    // using an ByteString.
    private static unsafe void CopyOnClickSelectable( ByteString text )
    {
        if( ImGuiNative.igSelectable_Bool( text.Path, 0, ImGuiSelectableFlags.None, Vector2.Zero ) != 0 )
        {
            ImGuiNative.igSetClipboardText( text.Path );
        }

        if( ImGui.IsItemHovered() )
        {
            ImGui.SetTooltip( "Click to copy to clipboard." );
        }
    }

    private sealed class CollectionSelector : FilterComboCache< ModCollection >
    {
        public CollectionSelector( Func<IReadOnlyList<ModCollection>> items )
            : base( items )
        { }

        public void Draw( string label, float width, int individualIdx )
        {
            var (_, collection) = Penumbra.CollectionManager.Individuals[ individualIdx ];
            if( Draw( label, collection.Name, width, ImGui.GetTextLineHeightWithSpacing() ) && CurrentSelection != null )
            {
                Penumbra.CollectionManager.SetCollection( CurrentSelection, CollectionType.Individual, individualIdx );
            }
        }

        public void Draw( string label, float width, CollectionType type )
        {
            var current = Penumbra.CollectionManager.ByType( type, ActorIdentifier.Invalid );
            if( Draw( label, current?.Name ?? string.Empty, width, ImGui.GetTextLineHeightWithSpacing() ) && CurrentSelection != null )
            {
                Penumbra.CollectionManager.SetCollection( CurrentSelection, type );
            }
        }

        protected override string ToString( ModCollection obj )
            => obj.Name;
    }

    private static readonly CollectionSelector CollectionsWithEmpty = new(() => Penumbra.CollectionManager.OrderBy( c => c.Name ).Prepend( ModCollection.Empty ).ToList());
    private static readonly CollectionSelector Collections          = new(() => Penumbra.CollectionManager.OrderBy( c => c.Name ).ToList());

    // Draw a collection selector of a certain width for a certain type.
    private static void DrawCollectionSelector( string label, float width, CollectionType collectionType, bool withEmpty )
        => ( withEmpty ? CollectionsWithEmpty : Collections ).Draw( label, width, collectionType );

    // Set up the file selector with the right flags and custom side bar items.
    public static FileDialogManager SetupFileManager()
    {
        var fileManager = new FileDialogManager
        {
            AddedWindowFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking,
        };

        if( Functions.GetDownloadsFolder( out var downloadsFolder ) )
        {
            fileManager.CustomSideBarItems.Add( ( "Downloads", downloadsFolder, FontAwesomeIcon.Download, -1 ) );
        }

        if( Functions.GetQuickAccessFolders( out var folders ) )
        {
            foreach( var ((name, path), idx) in folders.WithIndex() )
            {
                fileManager.CustomSideBarItems.Add( ( $"{name}##{idx}", path, FontAwesomeIcon.Folder, -1 ) );
            }
        }

        // Add Penumbra Root. This is not updated if the root changes right now.
        fileManager.CustomSideBarItems.Add( ( "Root Directory", Penumbra.Config.ModDirectory, FontAwesomeIcon.Gamepad, 0 ) );

        // Remove Videos and Music.
        fileManager.CustomSideBarItems.Add( ( "Videos", string.Empty, 0, -1 ) );
        fileManager.CustomSideBarItems.Add( ( "Music", string.Empty, 0, -1 ) );

        return fileManager;
    }
}