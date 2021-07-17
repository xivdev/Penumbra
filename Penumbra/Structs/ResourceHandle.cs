using System.Runtime.InteropServices;

namespace Penumbra.Structs
{
    [StructLayout( LayoutKind.Explicit )]
    public unsafe struct ResourceHandle
    {
        public const int SsoSize = 15;

        public byte* FileName()
        {
            if( FileNameLength > SsoSize )
            {
                return _fileName;
            }

            fixed( byte** name = &_fileName )
            {
                return ( byte* )name;
            }
        }

        [FieldOffset( 0x48 )]
        private byte* _fileName;

        [FieldOffset( 0x58 )]
        public int FileNameLength;
    }
}