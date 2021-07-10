using System;
using System.Runtime.InteropServices;

namespace Penumbra.Util
{
    public static class MemoryPermissions
    {
        [Flags]
        public enum MemoryProtection
        {
            Execute          = 0x10,
            ExecuteRead      = 0x20,
            ExecuteReadWrite = 0x40,
            ExecuteWriteCopy = 0x80,
            NoAccess         = 0x01,
            ReadOnly         = 0x02,
            ReadWrite        = 0x04,
            WriteCopy        = 0x08,
            TargetsInvalid   = 0x40000000,
            TargetsNoUpdate  = TargetsInvalid,
            Guard            = 0x100,
            NoCache          = 0x200,
            WriteCombine     = 0x400,
        }

        public static MemoryProtection ChangePermission( IntPtr memoryAddress, int length, MemoryProtection newPermissions )
        {
            var result = VirtualProtect( memoryAddress, ( UIntPtr )length, newPermissions, out var oldPermissions );

            if( !result )
            {
                throw new Exception(
                    $"Unable to change permissions at 0x{memoryAddress.ToInt64():X16} of length {length} and permission {newPermissions} (result={result})" );
            }

            var last = Marshal.GetLastWin32Error();
            if( last > 0 )
            {
                throw new Exception(
                    $"Unable to change permissions at 0x{memoryAddress.ToInt64():X16} of length {length} and permission {newPermissions} (error={last})" );
            }

            return oldPermissions;
        }

        public static void ChangePermission( IntPtr memoryAddress, int length, MemoryProtection newPermissions,
            out MemoryProtection oldPermissions )
            => oldPermissions = ChangePermission( memoryAddress, length, newPermissions );

        [DllImport( "kernel32.dll", SetLastError = true, ExactSpelling = true )]
        public static extern bool VirtualProtect(
            IntPtr lpAddress,
            UIntPtr dwSize,
            MemoryProtection flNewProtection,
            out MemoryProtection lpflOldProtect );
    }
}