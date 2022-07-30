using System;
using Penumbra.GameData.Util;

namespace Penumbra.GameData.Structs;

public unsafe struct CustomizeData : IEquatable< CustomizeData >
{
    public const int Size = 26;

    public fixed byte Data[Size];

    public void Read( void* source )
    {
        fixed( byte* ptr = Data )
        {
            Functions.MemCpyUnchecked( ptr, source, Size );
        }
    }

    public void Write( void* target )
    {
        fixed( byte* ptr = Data )
        {
            Functions.MemCpyUnchecked( target, ptr, Size );
        }
    }

    public CustomizeData Clone()
    {
        var ret = new CustomizeData();
        Write( ret.Data );
        return ret;
    }

    public bool Equals( CustomizeData other )
    {
        fixed( byte* ptr = Data )
        {
            return Functions.MemCmpUnchecked( ptr, other.Data, Size ) == 0;
        }
    }

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
}