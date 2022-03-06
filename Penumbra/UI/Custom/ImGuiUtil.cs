using System.Numerics;
using System.Windows.Forms;
using Dalamud.Interface;
using ImGuiNET;
using Penumbra.GameData.ByteString;

namespace Penumbra.UI.Custom
{
    public static partial class ImGuiCustom
    {
        public static void CopyOnClickSelectable( string text )
        {
            if( ImGui.Selectable( text ) )
            {
                Clipboard.SetText( text );
            }

            if( ImGui.IsItemHovered() )
            {
                ImGui.SetTooltip( "Click to copy to clipboard." );
            }
        }

        public static unsafe void CopyOnClickSelectable( Utf8String text )
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
    }

    public static partial class ImGuiCustom
    {
        public static void VerticalDistance( float distance )
        {
            ImGui.SetCursorPosY( ImGui.GetCursorPosY() + distance * ImGuiHelpers.GlobalScale );
        }

        public static void RightJustifiedText( float pos, string text )
        {
            ImGui.SetCursorPosX( pos - ImGui.CalcTextSize( text ).X - 2 * ImGui.GetStyle().ItemSpacing.X );
            ImGui.Text( text );
        }

        public static void RightJustifiedLabel( float pos, string text )
        {
            ImGui.SetCursorPosX( pos - ImGui.CalcTextSize( text ).X - ImGui.GetStyle().ItemSpacing.X / 2 );
            ImGui.Text( text );
            ImGui.SameLine( pos );
        }
    }

    public static partial class ImGuiCustom
    {
        public static void HoverTooltip( string text )
        {
            if( ImGui.IsItemHovered() )
            {
                ImGui.SetTooltip( text );
            }
        }
    }

    public static partial class ImGuiCustom
    {
        public static bool DisableButton( string label, bool condition )
        {
            using var alpha = ImGuiRaii.PushStyle( ImGuiStyleVar.Alpha, 0.5f, !condition );
            return ImGui.Button( label ) && condition;
        }
    }

    public static partial class ImGuiCustom
    {
        public static void PrintIcon( FontAwesomeIcon icon )
        {
            ImGui.PushFont( UiBuilder.IconFont );
            ImGui.TextUnformatted( icon.ToIconString() );
            ImGui.PopFont();
        }
    }
}