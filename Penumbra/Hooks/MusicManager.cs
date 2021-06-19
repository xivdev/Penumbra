using System;
using Dalamud.Plugin;

namespace Penumbra.Hooks
{
    public unsafe class MusicManager
    {
        private readonly IntPtr _musicManager;

        public MusicManager( Plugin plugin )
        {
            var scanner   = plugin!.PluginInterface!.TargetModuleScanner;
            var framework = plugin.PluginInterface.Framework.Address.BaseAddress;

            // the wildcard is basically the framework offset we want (lol)
            // .text:000000000009051A 48 8B 8E 18 2A 00 00      mov rcx,  [rsi+2A18h]
            // .text:0000000000090521 39 78 20                  cmp     [rax+20h], edi
            // .text:0000000000090524 0F 94 C2                  setz    dl
            // .text:0000000000090527 45 33 C0                  xor     r8d, r8d
            // .text:000000000009052A E8 41 1C 15 00            call    musicInit
            var musicInitCallLocation = scanner.ScanText( "48 8B 8E ?? ?? ?? ?? 39 78 20 0F 94 C2 45 33 C0" );
            var musicManagerOffset    = *( int* )( musicInitCallLocation + 3 );
            PluginLog.Debug( "Found MusicInitCall location at 0x{Location:X16}. Framework offset for MusicManager is 0x{Offset:X8}",
                musicInitCallLocation.ToInt64(), musicManagerOffset );
            _musicManager = *( IntPtr* )( framework + musicManagerOffset );
            PluginLog.Debug( "MusicManager found at 0x{Location:X16}", _musicManager );
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
}