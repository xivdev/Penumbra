using System;
using Penumbra.String.Functions;

namespace Penumbra.GameData.Structs;

public unsafe struct CustomizeData : IEquatable< CustomizeData >
{
    public const int Size = 26;

    public fixed byte Data[Size];

    public void Read( void* source )
    {
        fixed( byte* ptr = Data )
        {
            MemoryUtility.MemCpyUnchecked( ptr, source, Size );
        }
    }

    public readonly void Write( void* target )
    {
        fixed( byte* ptr = Data )
        {
            MemoryUtility.MemCpyUnchecked( target, ptr, Size );
        }
    }

    public readonly CustomizeData Clone()
    {
        var ret = new CustomizeData();
        Write( ret.Data );
        return ret;
    }

    public readonly bool Equals( CustomizeData other )
    {
        fixed( byte* ptr = Data )
        {
            return MemoryUtility.MemCmpUnchecked( ptr, other.Data, Size ) == 0;
        }
    }

    public static bool Equals( CustomizeData* lhs, CustomizeData* rhs )
        => MemoryUtility.MemCmpUnchecked( lhs, rhs, Size ) == 0;

    public override bool Equals( object? obj )
        => obj is CustomizeData other && Equals( other );

    public override int GetHashCode()
    {
        fixed( byte* ptr = Data )
        {
            var p = ( int* )ptr;
            var u = *( ushort* )( p + 6 );
            return HashCode.Combine( *p, p[ 1 ], p[ 2 ], p[ 3 ], p[ 4 ], p[ 5 ], u );
        }
    }

    public string WriteBase64()
    {
        fixed( byte* ptr = Data )
        {
            var data = new ReadOnlySpan< byte >( ptr, Size );
            return Convert.ToBase64String( data );
        }
    }

    public bool LoadBase64( string base64 )
    {
        var buffer = stackalloc byte[Size];
        var span   = new Span< byte >( buffer, Size );
        if( !Convert.TryFromBase64String( base64, span, out var written ) || written != Size )
        {
            return false;
        }

        Read( buffer );
        return true;
    }
}