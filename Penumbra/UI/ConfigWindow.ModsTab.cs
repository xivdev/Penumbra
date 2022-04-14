using System.Numerics;
using OtterGui.Raii;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    public void DrawModsTab()
    {
        using var tab = ImRaii.TabItem( "Mods" );
        if( !tab )
        {
            return;
        }

        using var child = ImRaii.Child( "##ModsTab", -Vector2.One );
        if( !child )
        {
            return;
        }
    }
}