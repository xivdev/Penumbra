using System.Windows.Forms;
using ImGuiNET;

namespace Penumbra.UI
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
    }

    public static partial class ImGuiCustom
    {
        public static void VerticalDistance( float distance )
        {
            ImGui.SetCursorPosY( ImGui.GetCursorPosY() + distance );
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
}