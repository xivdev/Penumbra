using System.Runtime.InteropServices;
using OtterGui.Raii;
using Penumbra.Mods;

namespace Penumbra.UI;

[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct ModState
{
    public uint Color;
}

public partial class SettingsInterface
{
    private class TabBrowser
    {
        private readonly ModFileSystemA        _fileSystem;
        private readonly ModFileSystemSelector _selector;

        public TabBrowser()
        {
            _fileSystem = ModFileSystemA.Load();
            _selector   = new ModFileSystemSelector( _fileSystem );
        }

        public void Draw()
        {
            using var ret = ImRaii.TabItem( "Available Mods" );
            if( !ret )
            {
                return;
            }

            _selector.Draw( 400 );
        }
    }
}