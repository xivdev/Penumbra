using System;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using Lumina.Data.Parsing;
using Lumina.Excel.GeneratedSheets;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Collections;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    // Draw text given by a Utf8String.
    internal static unsafe void Text( Utf8String s )
        => ImGuiNative.igTextUnformatted( s.Path, s.Path + s.Length );

    // Draw text given by a byte pointer.
    internal static unsafe void Text( byte* s, int length )
        => ImGuiNative.igTextUnformatted( s, s + length );

    // Draw the name of a resource file.
    internal static unsafe void Text( ResourceHandle* resource )
        => Text( resource->FileName(), resource->FileNameLength );

    // Draw a changed item, invoking the Api-Events for clicks and tooltips.
    // Also draw the item Id in grey
    internal void DrawChangedItem( string name, object? data, float itemIdOffset = 0 )
    {
        var ret = ImGui.Selectable( name ) ? MouseButton.Left : MouseButton.None;
        ret = ImGui.IsItemClicked( ImGuiMouseButton.Right ) ? MouseButton.Right : ret;
        ret = ImGui.IsItemClicked( ImGuiMouseButton.Middle ) ? MouseButton.Middle : ret;

        if( ret != MouseButton.None )
        {
            _penumbra.Api.InvokeClick( ret, data );
        }

        if( _penumbra.Api.HasTooltip && ImGui.IsItemHovered() )
        {
            using var tt = ImRaii.Tooltip();
            _penumbra.Api.InvokeTooltip( data );
        }

        if( data is Item it )
        {
            ImGui.SameLine( ImGui.GetContentRegionAvail().X );
            ImGuiUtil.RightJustify( $"({( ( Quad )it.ModelMain ).A})", ColorId.ItemId.Value() );
        }
    }

    // A selectable that copies its text to clipboard on selection and provides a on-hover tooltip about that,
    // using an Utf8String.
    internal static unsafe void CopyOnClickSelectable( Utf8String text )
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

    // Draw a collection selector of a certain width for a certain type.
    private static void DrawCollectionSelector( string label, float width, ModCollection.Type type, bool withEmpty, string? characterName )
    {
        ImGui.SetNextItemWidth( width );
        var current = type switch
        {
            ModCollection.Type.Default   => Penumbra.CollectionManager.Default,
            ModCollection.Type.Character => Penumbra.CollectionManager.Character( characterName ?? string.Empty ),
            ModCollection.Type.Current   => Penumbra.CollectionManager.Current,
            _                            => throw new ArgumentOutOfRangeException( nameof( type ), type, null ),
        };

        using var combo = ImRaii.Combo( label, current.Name );
        if( !combo )
        {
            return;
        }

        foreach( var collection in Penumbra.CollectionManager.GetEnumeratorWithEmpty().Skip( withEmpty ? 0 : 1 ) )
        {
            using var id = ImRaii.PushId( collection.Index );
            if( ImGui.Selectable( collection.Name, collection == current ) )
            {
                Penumbra.CollectionManager.SetCollection( collection, type, characterName );
            }
        }
    }
}