using System;
using Dalamud.Logging;

namespace Penumbra.Util
{
    public static class GeneralUtil
    {
        public static void PrintDebugAddress( string name, IntPtr address )
        {
            var module = Dalamud.SigScanner.Module.BaseAddress.ToInt64();
            PluginLog.Debug( "{Name} found at 0x{Address:X16}, +0x{Offset:X}", name, address.ToInt64(), address.ToInt64() - module );
        }
    }
}