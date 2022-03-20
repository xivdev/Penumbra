using System;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;

namespace Penumbra.Interop;

// Use this to disable streaming of specific soundfiles,
// which will allow replacement of .scd files.
public unsafe class MusicManager
{
    // The wildcard is the offset in framework to the MusicManager in Framework.
    [Signature( "48 8B 8E ?? ?? ?? ?? 39 78 20 0F 94 C2 45 33 C0", ScanType = ScanType.Text )]
    private readonly IntPtr _musicInitCallLocation = IntPtr.Zero;

    private readonly IntPtr _musicManager;

    public MusicManager()
    {
        SignatureHelper.Initialise( this );
        var framework          = Dalamud.Framework.Address.BaseAddress;
        var musicManagerOffset = *( int* )( _musicInitCallLocation + 3 );
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