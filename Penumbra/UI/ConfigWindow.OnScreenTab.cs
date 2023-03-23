using System;
using OtterGui.Widgets;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    private class OnScreenTab : ITab
    {
        private ResourceTreeViewer? _viewer;

        public ReadOnlySpan<byte> Label
            => "On-Screen"u8;

        public void DrawContent()
        {
            _viewer ??= new( "On-Screen tab", 0, delegate { }, delegate { } );

            _viewer.Draw();
        }
    }
}
