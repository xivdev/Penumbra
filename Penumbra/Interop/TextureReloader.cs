using Dalamud.Game;
using Penumbra.Structs;
using System;
using System.Runtime.InteropServices;

namespace Penumbra.Interop {
    public unsafe class TextureReloader {
        [UnmanagedFunctionPointer( CallingConvention.ThisCall )]
        private unsafe delegate void* RequestFileDelegate( IntPtr a1, IntPtr a2, IntPtr a3, byte a4 );
        private readonly RequestFileDelegate RequestFile;

        [UnmanagedFunctionPointer( CallingConvention.Cdecl )]
        private unsafe delegate IntPtr GetFileManagerDelegate();
        private readonly GetFileManagerDelegate GetFileManager;

        [UnmanagedFunctionPointer( CallingConvention.ThisCall )]
        private unsafe delegate IntPtr ReloadTextureDelegate( IntPtr a1 );
        private readonly ReloadTextureDelegate ReloadTextureResource;

        public TextureReloader(SigScanner sigScanner) {
            GetFileManager = Marshal.GetDelegateForFunctionPointer<GetFileManagerDelegate>( sigScanner.ScanText( "E8 ?? ?? ?? ?? 4C 8B 2D ?? ?? ?? ?? 49 8B CD" ) );
            RequestFile = Marshal.GetDelegateForFunctionPointer<RequestFileDelegate>( sigScanner.ScanText( "E8 ?? ?? ?? ?? F0 FF 4F 5C 48 8D 4F 30" ) );
            ReloadTextureResource = Marshal.GetDelegateForFunctionPointer<ReloadTextureDelegate>( sigScanner.ScanText( "40 53 48 83 EC 20 48 83 B9 ?? ?? ?? ?? ?? 48 8B D9 74 52" ) );
        }

        public void RefreshTexture( TextureResourceHandle* handle) {
            if( handle == null ) return;

            RequestFile( GetFileManager(), ( IntPtr )handle->Unk, ( IntPtr )handle, 1 );

            if( handle->NewKernelTexture == null || handle->NewKernelTexture == handle->KernelTexture ) return;

            ReloadTextureResource( ( IntPtr )handle );
        }
    }
}
