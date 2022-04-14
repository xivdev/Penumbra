using System.Numerics;
using OtterGui.Raii;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    public void DrawCollectionsTab()
    {
        using var tab = ImRaii.TabItem( "Collections" );
        if( !tab )
        {
            return;
        }

        using var child = ImRaii.Child( "##CollectionsTab", -Vector2.One );
        if( !child )
        {
            return;
        }
    }
}