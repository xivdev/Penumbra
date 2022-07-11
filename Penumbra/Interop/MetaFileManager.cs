using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using Penumbra.GameData.ByteString;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;

namespace Penumbra.Interop;

public unsafe class MetaFileManager : IDisposable
{
    public MetaFileManager()
    {
        SignatureHelper.Initialise( this );
        InitImc();
    }

    public void Dispose()
    {
        DisposeImc();
    }


    // Allocate in the games space for file storage.
    // We only need this if using any meta file.
    [Signature( "E8 ?? ?? ?? ?? 41 B9 ?? ?? ?? ?? 4C 8B C0" )]
    public IntPtr GetFileSpaceAddress;

    public IMemorySpace* GetFileSpace()
        => ( ( delegate* unmanaged< IMemorySpace* > )GetFileSpaceAddress )();

    public void* AllocateFileMemory( ulong length, ulong alignment = 0 )
        => GetFileSpace()->Malloc( length, alignment );

    public void* AllocateFileMemory( int length, int alignment = 0 )
        => AllocateFileMemory( ( ulong )length, ( ulong )alignment );


    // We only need this for IMC files, since we need to hook their cleanup function.
    [Signature( "48 8D 05 ?? ?? ?? ?? 48 8B 8B ?? ?? ?? ?? 48 89 03", ScanType = ScanType.StaticAddress )]
    public IntPtr* DefaultResourceHandleVTable;

    public delegate void                  ClearResource( ResourceHandle* resource );
    public          Hook< ClearResource > ClearDefaultResourceHook = null!;

    private readonly Dictionary< IntPtr, (ImcFile, IntPtr, int) > _originalImcData = new();
    private readonly Dictionary< ImcFile, IntPtr >       _currentUse      = new();

    // We store the original data of loaded IMCs so that we can restore it before they get destroyed,
    // similar to the other meta files, just with arbitrary destruction.
    private void ClearDefaultResourceDetour( ResourceHandle* resource )
    {
        if( _originalImcData.TryGetValue( ( IntPtr )resource, out var data ) )
        {
            ClearImcData( resource, data.Item1, data.Item2, data.Item3);
        }

        ClearDefaultResourceHook.Original( resource );
    }

    // Reset all files from a given IMC cache if they exist.
    public void ResetByFile( ImcFile file )
    {
        if( !_currentUse.TryGetValue( file, out var resource ) )
        {
            return;
        }

        if( _originalImcData.TryGetValue( resource, out var data ) )
        {
            ClearImcData((ResourceHandle*) resource, file, data.Item2, data.Item3  );
        }
        else
        {
            _currentUse.Remove( file );
        }
    }

    // Clear a single IMC resource and reset it to its original data.
    private void ClearImcData( ResourceHandle* resource, ImcFile file, IntPtr data, int length)
    {
        var name = new FullPath( Utf8String.FromSpanUnsafe( resource->FileNameSpan(), true ).ToString() );
        PluginLog.Debug( "Restoring data of {$Name:l} (0x{Resource}) to 0x{Data:X} and Length {Length} before deletion.", name,
            ( ulong )resource, ( ulong )data, length );
        resource->SetData( data, length );
        _originalImcData.Remove( ( IntPtr )resource );
        _currentUse.Remove( file );
    }

    // Called when a new IMC is manipulated to store its data.
    public void AddImcFile( ResourceHandle* resource, ImcFile file, IntPtr data, int length)
    {
        PluginLog.Debug( "Storing data 0x{Data:X} of Length {Length} for {$Name:l} (0x{Resource:X}).", ( ulong )data, length,
            Utf8String.FromSpanUnsafe( resource->FileNameSpan(), true, null, null ), ( ulong )resource );
        _originalImcData[ ( IntPtr )resource ] = ( file, data, length );
        _currentUse[ file ]                    = ( IntPtr )resource;
    }

    // Initialize the hook at VFunc 25, which is called when default resources (and IMC resources do not overwrite it) destroy their data.
    private void InitImc()
    {
        ClearDefaultResourceHook = new Hook< ClearResource >( DefaultResourceHandleVTable[ 25 ], ClearDefaultResourceDetour );
        ClearDefaultResourceHook.Enable();
    }

    private void DisposeImc()
    {
        ClearDefaultResourceHook.Disable();
        ClearDefaultResourceHook.Dispose();
        // Restore all IMCs to their default values on dispose.
        // This should only be relevant when testing/disabling/reenabling penumbra.
        foreach( var (resourcePtr, (file, data, length)) in _originalImcData )
        {
            var resource = ( ResourceHandle* )resourcePtr;
            resource->SetData( data, length );
        }

        _originalImcData.Clear();
        _currentUse.Clear();
    }
}