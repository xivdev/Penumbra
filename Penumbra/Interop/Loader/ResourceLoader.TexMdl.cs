using System;
using System.Collections.Generic;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using Penumbra.GameData;
using Penumbra.GameData.Enums;
using Penumbra.String.Classes;

namespace Penumbra.Interop.Loader;

// Since 6.0, Mdl and Tex Files require special treatment, probably due to datamining protection.
public unsafe partial class ResourceLoader
{
    // Custom ulong flag to signal our files as opposed to SE files.
    public static readonly IntPtr CustomFileFlag = new(0xDEADBEEF);

    // We need to keep a list of all CRC64 hash values of our replaced Mdl and Tex files,
    // i.e. CRC32 of filename in the lower bytes, CRC32 of parent path in the upper bytes.
    private readonly HashSet< ulong > _customFileCrc = new();

    public IReadOnlySet< ulong > CustomFileCrc
        => _customFileCrc;


    // The function that checks a files CRC64 to determine whether it is 'protected'.
    // We use it to check against our stored CRC64s and if it corresponds, we return the custom flag.
    public delegate IntPtr CheckFileStatePrototype( IntPtr unk1, ulong crc64 );

    [Signature( Sigs.CheckFileState, DetourName = nameof( CheckFileStateDetour ) )]
    public readonly Hook< CheckFileStatePrototype > CheckFileStateHook = null!;

    private IntPtr CheckFileStateDetour( IntPtr ptr, ulong crc64 )
        => _customFileCrc.Contains( crc64 ) ? CustomFileFlag : CheckFileStateHook.Original( ptr, crc64 );


    // We use the local functions for our own files in the extern hook.
    public delegate byte LoadTexFileLocalDelegate( ResourceHandle* handle, int unk1, IntPtr unk2, bool unk3 );

    [Signature( Sigs.LoadTexFileLocal )]
    public readonly LoadTexFileLocalDelegate LoadTexFileLocal = null!;

    public delegate byte LoadMdlFileLocalPrototype( ResourceHandle* handle, IntPtr unk1, bool unk2 );

    [Signature( Sigs.LoadMdlFileLocal )]
    public readonly LoadMdlFileLocalPrototype LoadMdlFileLocal = null!;


    // We hook the extern functions to just return the local one if given the custom flag as last argument.
    public delegate byte LoadTexFileExternPrototype( ResourceHandle* handle, int unk1, IntPtr unk2, bool unk3, IntPtr unk4 );

    [Signature( Sigs.LoadTexFileExtern, DetourName = nameof( LoadTexFileExternDetour ) )]
    public readonly Hook< LoadTexFileExternPrototype > LoadTexFileExternHook = null!;

    private byte LoadTexFileExternDetour( ResourceHandle* resourceHandle, int unk1, IntPtr unk2, bool unk3, IntPtr ptr )
        => ptr.Equals( CustomFileFlag )
            ? LoadTexFileLocal.Invoke( resourceHandle, unk1, unk2, unk3 )
            : LoadTexFileExternHook.Original( resourceHandle, unk1, unk2, unk3, ptr );

    public delegate byte LoadMdlFileExternPrototype( ResourceHandle* handle, IntPtr unk1, bool unk2, IntPtr unk3 );


    [Signature( Sigs.LoadMdlFileExtern, DetourName = nameof( LoadMdlFileExternDetour ) )]
    public readonly Hook< LoadMdlFileExternPrototype > LoadMdlFileExternHook = null!;

    private byte LoadMdlFileExternDetour( ResourceHandle* resourceHandle, IntPtr unk1, bool unk2, IntPtr ptr )
        => ptr.Equals( CustomFileFlag )
            ? LoadMdlFileLocal.Invoke( resourceHandle, unk1, unk2 )
            : LoadMdlFileExternHook.Original( resourceHandle, unk1, unk2, ptr );


    private void AddCrc( Utf8GamePath _, ResourceType type, FullPath? path, object? _2 )
    {
        if( path.HasValue && type is ResourceType.Mdl or ResourceType.Tex )
        {
            _customFileCrc.Add( path.Value.Crc64 );
        }
    }

    private void EnableTexMdlTreatment()
    {
        PathResolved += AddCrc;
        CheckFileStateHook.Enable();
        LoadTexFileExternHook.Enable();
        LoadMdlFileExternHook.Enable();
    }

    private void DisableTexMdlTreatment()
    {
        PathResolved -= AddCrc;
        _customFileCrc.Clear();
        _customFileCrc.TrimExcess();
        CheckFileStateHook.Disable();
        LoadTexFileExternHook.Disable();
        LoadMdlFileExternHook.Disable();
    }

    private void DisposeTexMdlTreatment()
    {
        CheckFileStateHook.Dispose();
        LoadTexFileExternHook.Dispose();
        LoadMdlFileExternHook.Dispose();
    }
}