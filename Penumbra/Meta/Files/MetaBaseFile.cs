using System;
using Dalamud.Memory;

namespace Penumbra.Meta.Files;

public unsafe class MetaBaseFile : IDisposable
{
    public byte* Data { get; private set; }
    public int Length { get; private set; }
    public int Index { get; }

    public MetaBaseFile( int idx )
        => Index = idx;

    protected (IntPtr Data, int Length) DefaultData
        => Penumbra.CharacterUtility.DefaultResource( Index );

    // Reset to default values.
    public virtual void Reset()
    { }

    // Obtain memory.
    protected void AllocateData( int length )
    {
        Length = length;
        Data   = ( byte* )MemoryHelper.GameAllocateDefault( ( ulong )length );
        ;
        GC.AddMemoryPressure( length );
    }

    // Free memory.
    protected void ReleaseUnmanagedResources()
    {
        var ptr = ( IntPtr )Data;
        MemoryHelper.GameFree( ref ptr, ( ulong )Length );
        GC.RemoveMemoryPressure( Length );
        Length = 0;
        Data   = null;
    }

    // Manually free memory. 
    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize( this );
    }

    ~MetaBaseFile()
    {
        ReleaseUnmanagedResources();
    }
}