using System;
using System.Collections.Generic;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
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

    [Signature( "E8 ?? ?? ?? ?? 48 85 c0 74 ?? 45 0f b6 ce 48 89 44 24", DetourName = nameof( CheckFileStateDetour ) )]
    public Hook< CheckFileStatePrototype > CheckFileStateHook = null!;

    private IntPtr CheckFileStateDetour( IntPtr ptr, ulong crc64 )
        => _customFileCrc.Contains( crc64 ) ? CustomFileFlag : CheckFileStateHook.Original( ptr, crc64 );


    // We use the local functions for our own files in the extern hook.
    public delegate byte LoadTexFileLocalDelegate( ResourceHandle* handle, int unk1, IntPtr unk2, bool unk3 );

    [Signature( "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 30 49 8B F0 44 88 4C 24 20" )]
    public LoadTexFileLocalDelegate LoadTexFileLocal = null!;

    public delegate byte LoadMdlFileLocalPrototype( ResourceHandle* handle, IntPtr unk1, bool unk2 );

    [Signature( "40 55 53 56 57 41 56 41 57 48 8D 6C 24 D1 48 81 EC 98 00 00 00" )]
    public LoadMdlFileLocalPrototype LoadMdlFileLocal = null!;


    // We hook the extern functions to just return the local one if given the custom flag as last argument.
    public delegate byte LoadTexFileExternPrototype( ResourceHandle* handle, int unk1, IntPtr unk2, bool unk3, IntPtr unk4 );

    [Signature( "E8 ?? ?? ?? ?? 0F B6 E8 48 8B CB E8", DetourName = nameof( LoadTexFileExternDetour ) )]
    public Hook< LoadTexFileExternPrototype > LoadTexFileExternHook = null!;

    private byte LoadTexFileExternDetour( ResourceHandle* resourceHandle, int unk1, IntPtr unk2, bool unk3, IntPtr ptr )
        => ptr.Equals( CustomFileFlag )
            ? LoadTexFileLocal.Invoke( resourceHandle, unk1, unk2, unk3 )
            : LoadTexFileExternHook.Original( resourceHandle, unk1, unk2, unk3, ptr );

    public delegate byte LoadMdlFileExternPrototype( ResourceHandle* handle, IntPtr unk1, bool unk2, IntPtr unk3 );


    [Signature( "E8 ?? ?? ?? ?? EB 02 B0 F1", DetourName = nameof( LoadMdlFileExternDetour ) )]
    public Hook< LoadMdlFileExternPrototype > LoadMdlFileExternHook = null!;

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