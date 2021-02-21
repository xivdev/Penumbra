using System;
using System.Runtime.InteropServices;

namespace Penumbra.Structs
{
    [StructLayout( LayoutKind.Sequential )]
    public unsafe struct CharacterResourceManager
    {
        public void* VTable;

        public IntPtr Resources; // Size: 85, I hate C#
    }
}