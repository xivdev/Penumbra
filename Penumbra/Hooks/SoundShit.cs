using System;
using System.Runtime.InteropServices;
using Dalamud.Plugin;
using Reloaded.Hooks;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.X64;

namespace Penumbra.Hooks
{
    public unsafe class SoundShit
    {
        private readonly IntPtr _musicManager;

        public SoundShit( Plugin plugin )
        {
            var scanner = plugin!.PluginInterface!.TargetModuleScanner;

            var fw = plugin.PluginInterface.Framework.Address.BaseAddress;

            // the wildcard is basically the framework offset we want (lol)
            // .text:000000000009051A 48 8B 8E 18 2A 00 00      mov rcx,  [rsi+2A18h]
            // .text:0000000000090521 39 78 20                  cmp     [rax+20h], edi
            // .text:0000000000090524 0F 94 C2                  setz    dl
            // .text:0000000000090527 45 33 C0                  xor     r8d, r8d
            // .text:000000000009052A E8 41 1C 15 00            call    musicInit
            var shit   = scanner.ScanText( "48 8B 8E ?? ?? ?? ?? 39 78 20 0F 94 C2 45 33 C0" );
            var fuckkk = *( int* )( shit + 3 );
            _musicManager    = *( IntPtr* )( fw + fuckkk );
            StreamingEnabled = false;

            // PluginLog.Information("disabled streaming: {addr}", _musicManager);
        }

        public bool StreamingEnabled
        {
            get => *( bool* )( _musicManager + 50 );
            set => *( bool* )( _musicManager + 50 ) = value;
        }
    }
}