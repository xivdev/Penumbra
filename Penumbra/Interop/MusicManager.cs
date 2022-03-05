using System;
using Dalamud.Logging;

namespace Penumbra.Interop;

// Use this to disable streaming of specific soundfiles,
// which will allow replacement of .scd files.
public unsafe class MusicManager
{
    private readonly IntPtr _musicManager;

    public MusicManager()
    {
        var framework = Dalamud.Framework.Address.BaseAddress;
        // The wildcard is the offset in framework to the MusicManager in Framework.
        var musicInitCallLocation = Dalamud.SigScanner.ScanText( "48 8B 8E ?? ?? ?? ?? 39 78 20 0F 94 C2 45 33 C0" );
        var musicManagerOffset    = *( int* )( musicInitCallLocation + 3 );
        PluginLog.Debug( "Found MusicInitCall location at 0x{Location:X16}. Framework offset for MusicManager is 0x{Offset:X8}",
            musicInitCallLocation.ToInt64(), musicManagerOffset );
        _musicManager = *( IntPtr* )( framework + musicManagerOffset );
        PluginLog.Debug( "MusicManager found at 0x{Location:X16}", _musicManager.ToInt64() );
    }

    public bool StreamingEnabled
    {
        get => *( bool* )( _musicManager + 50 );
        private set
        {
            PluginLog.Debug( value ? "Music streaming enabled." : "Music streaming disabled." );
            *( bool* )( _musicManager + 50 ) = value;
        }
    }

    public void EnableStreaming()
        => StreamingEnabled = true;

    public void DisableStreaming()
        => StreamingEnabled = false;
}