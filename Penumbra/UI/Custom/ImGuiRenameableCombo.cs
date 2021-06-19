using ImGuiNET;

namespace Penumbra.UI.Custom
{
    public static partial class ImGuiCustom
    {
        public static bool RenameableCombo( string label, ref int currentItem, out string newName, string[] items, int numItems )
        {
            var ret = false;
            newName = "";
            var newOption = "";
            if( !ImGui.BeginCombo( label, numItems > 0 ? items[ currentItem ] : newOption ) )
            {
                return false;
            }

            for( var i = 0; i < numItems; ++i )
            {
                var isSelected = i == currentItem;
                ImGui.SetNextItemWidth( -1 );
                if( ImGui.InputText( $"##{label}_{i}", ref items[ i ], 64, ImGuiInputTextFlags.EnterReturnsTrue ) )
                {
                    currentItem = i;
                    newName     = items[ i ];
                    ret         = true;
                    ImGui.CloseCurrentPopup();
                }

                if( isSelected )
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.SetNextItemWidth( -1 );
            if( ImGui.InputTextWithHint( $"##{label}_new", "Add new item...", ref newOption, 64, ImGuiInputTextFlags.EnterReturnsTrue ) )
            {
                currentItem = numItems;
                newName     = newOption;
                ret         = true;
                ImGui.CloseCurrentPopup();
            }

            if( numItems == 0 )
            {
                ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();

            return ret;
        }
    }
}