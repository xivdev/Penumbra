using System;
using System.Runtime.InteropServices;
using System.Text;
using Penumbra.String.Functions;

namespace Penumbra.GameData.Structs;

[StructLayout(LayoutKind.Sequential, Size = Size)]
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

    public override bool Equals( object? obj )
        => obj is CustomizeData other && Equals( other );

    public static bool Equals(CustomizeData* lhs, CustomizeData* rhs)
        => MemoryUtility.MemCmpUnchecked(lhs, rhs, Size) == 0;

    /// <remarks>Compare Gender and then only from Height onwards, because all screen actors are set to Height 50,
    /// the Race is implicitly included in the subrace (after height),
    /// and the body type is irrelevant for players.</remarks>>
    public static bool ScreenActorEquals(CustomizeData* lhs, CustomizeData* rhs)
        => lhs->Data[1] == rhs->Data[1] && MemoryUtility.MemCmpUnchecked(lhs->Data + 4, rhs->Data + 4, Size - 4) == 0;

    public override int GetHashCode()
    {
        fixed( byte* ptr = Data )
        {
            var p = ( int* )ptr;
            var u = *( ushort* )( p + 6 );
            return HashCode.Combine( *p, p[ 1 ], p[ 2 ], p[ 3 ], p[ 4 ], p[ 5 ], u );
        }
    }

    public readonly string WriteBase64()
    {
        fixed( byte* ptr = Data )
        {
            var data = new ReadOnlySpan< byte >( ptr, Size );
            return Convert.ToBase64String( data );
        }
    }

    public override string ToString()
    {
        var sb = new StringBuilder(Size * 3);
        for (var i = 0; i < Size - 1; ++i)
        {
            sb.Append($"{Data[i]:X2} ");
        }
        sb.Append($"{Data[Size - 1]:X2}");
        return sb.ToString();
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