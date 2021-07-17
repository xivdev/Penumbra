using System.Collections.Generic;
using ImGuiNET;

namespace Penumbra.UI.Custom
{
    public static partial class ImGuiCustom
    {
        public static bool InputOrText( bool editable, string label, ref string text, uint maxLength )
        {
            if( editable )
            {
                return ResizingTextInput( label, ref text, maxLength );
            }

            ImGui.Text( text );
            return false;
        }

        public static bool ResizingTextInput( string label, ref string input, uint maxLength )
            => ResizingTextInputIntern( label, ref input, maxLength ).Item1;

        public static bool ResizingTextInput( ref string input, uint maxLength )
        {
            var (ret, id) = ResizingTextInputIntern( $"##{input}", ref input, maxLength );
            if( ret )
            {
                TextInputWidths.Remove( id );
            }

            return ret;
        }

        private static (bool, uint) ResizingTextInputIntern( string label, ref string input, uint maxLength )
        {
            var id = ImGui.GetID( label );
            if( !TextInputWidths.TryGetValue( id, out var width ) )
            {
                width = ImGui.CalcTextSize( input ).X + 10;
            }

            ImGui.SetNextItemWidth( width );
            var ret = ImGui.InputText( label, ref input, maxLength, ImGuiInputTextFlags.EnterReturnsTrue );
            TextInputWidths[ id ] = ImGui.CalcTextSize( input ).X + 10;
            return ( ret, id );
        }

        private static readonly Dictionary< uint, float > TextInputWidths = new();
    }
}