using System.Numerics;
using OtterGui.Raii;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    public void DrawEffectiveChangesTab()
    {
        if( !Penumbra.Config.ShowAdvanced )
        {
            return;
        }

        using var tab = ImRaii.TabItem( "Effective Changes" );
        if( !tab )
        {
            return;
        }

        using var child = ImRaii.Child( "##EffectiveChangesTab", -Vector2.One );
        if( !child )
        {
            return;
        }
    }
}