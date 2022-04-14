using System.Numerics;
using OtterGui.Raii;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    public void DrawResourceManagerTab()
    {
        if( !DebugTabVisible )
        {
            return;
        }

        using var tab = ImRaii.TabItem( "Resource Manager" );
        if( !tab )
        {
            return;
        }

        using var child = ImRaii.Child( "##ResourceManagerTab", -Vector2.One );
        if( !child )
        {
            return;
        }
    }
}