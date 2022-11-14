using ImGuiNET;
using OtterGui.Raii;
using Penumbra.GameData.Files;
using Penumbra.String.Classes;

namespace Penumbra.UI.Classes;

public partial class ModEditWindow
{
    private readonly FileEditor< MdlFile > _modelTab;

    private static bool DrawModelPanel( MdlFile file, bool disabled )
    {
        var ret = false;
        for( var i = 0; i < file.Materials.Length; ++i )
        {
            using var id  = ImRaii.PushId( i );
            var       tmp = file.Materials[ i ];
            if( ImGui.InputText( string.Empty, ref tmp, Utf8GamePath.MaxGamePathLength,
                   disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None )
            && tmp.Length > 0
            && tmp        != file.Materials[ i ] )
            {
                file.Materials[ i ] = tmp;
                ret                 = true;
            }
        }

        return !disabled && ret;
    }
}