using System;
using System.Runtime.InteropServices;

namespace Penumbra.Structs
{
    [StructLayout( LayoutKind.Sequential )]
    public unsafe struct CharacterUtility
    {
        public void* VTable;

        public IntPtr Resources; // Size: 85, I hate C#
    }
}