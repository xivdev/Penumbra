using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Penumbra.CrashHandler;

internal static partial class Win32Interop
{
    [Flags]
    private enum AccessRights : uint
    {
        QueryLimitedInformation = 0x00001000,
        Synchronize             = 0x00100000,
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint OpenProcess(AccessRights desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [LibraryImport("kernel32.dll", SetLastError = false)]
    private static partial int WaitForSingleObject(nint handle, uint milliseconds);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetExitCodeProcess(nint handle, out uint exitCode);

    [LibraryImport("kernel32.dll", SetLastError = false)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);

    public static uint WaitForExit(uint processId)
    {
        var handle = OpenProcess(AccessRights.Synchronize | AccessRights.QueryLimitedInformation, false, processId);
        if (handle == nint.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        try
        {
            var error = WaitForSingleObject(handle, uint.MaxValue);
            Marshal.ThrowExceptionForHR(error);
            if (!GetExitCodeProcess(handle, out var exitCode))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            return exitCode;
        }
        finally
        {
            CloseHandle(handle);
        }
    }
}
