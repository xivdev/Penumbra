using System;
using System.Runtime.InteropServices;

namespace Penumbra.Meta.Files;

public unsafe class MetaBaseFile : IDisposable
{
    public byte* Data { get; private set; }
    public int Length { get; private set; }
    public int Index { get; }

    public MetaBaseFile( int idx )
        => Index = idx;

    protected (IntPtr Data, int Length) DefaultData
        => Penumbra.CharacterUtility.DefaultResources[ Index ];

    // Reset to default values.
    public virtual void Reset()
    {}

    // Obtain memory.
    protected void AllocateData( int length )
    {
        Length = length;
        Data   = ( byte* )Marshal.AllocHGlobal( length );
        GC.AddMemoryPressure( length );
    }

    // Free memory.
    protected void ReleaseUnmanagedResources()
    {
        Marshal.FreeHGlobal( ( IntPtr )Data );
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