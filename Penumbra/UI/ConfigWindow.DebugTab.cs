using System.Numerics;
using OtterGui.Raii;

namespace Penumbra.UI;

public partial class ConfigWindow
{
#if DEBUG
    private const bool DefaultVisibility = true;
#else
    private const bool DefaultVisibility = false;
#endif

    public bool DebugTabVisible = DefaultVisibility;

    public void DrawDebugTab()
    {
        if( !DebugTabVisible )
        {
            return;
        }

        using var tab = ImRaii.TabItem( "Debug" );
        if( !tab )
        {
            return;
        }

        using var child = ImRaii.Child( "##DebugTab", -Vector2.One );
        if( !child )
        {
            return;
        }
    }
}