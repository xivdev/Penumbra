using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Penumbra.GameData.Util;

namespace Penumbra.Meta.Manipulations;

public interface IMetaManipulation
{
    public int FileIndex();
}

public interface IMetaManipulation< T > : IMetaManipulation, IComparable< T >, IEquatable< T > where T : struct
{ }

public struct ManipulationSet< T > where T : struct, IMetaManipulation< T >
{
    private List< T >? _data = null;

    public IReadOnlyList< T > Data
        => ( IReadOnlyList< T >? )_data ?? Array.Empty< T >();

    public int Count
        => _data?.Count ?? 0;

    public ManipulationSet( int count = 0 )
    {
        if( count > 0 )
        {
            _data = new List< T >( count );
        }
    }

    public bool TryAdd( T manip )
    {
        if( _data == null )
        {
            _data = new List< T > { manip };
            return true;
        }

        var idx = _data.BinarySearch( manip );
        if( idx >= 0 )
        {
            return false;
        }

        _data.Insert( ~idx, manip );
        return true;
    }

    public int Set( T manip )
    {
        if( _data == null )
        {
            _data = new List< T > { manip };
            return 0;
        }

        var idx = _data.BinarySearch( manip );
        if( idx >= 0 )
        {
            _data[ idx ] = manip;
            return idx;
        }

        idx = ~idx;
        _data.Insert( idx, manip );
        return idx;
    }

    public bool TryGet( T manip, out T value )
    {
        var idx = _data?.BinarySearch( manip ) ?? -1;
        if( idx < 0 )
        {
            value = default;
            return false;
        }

        value = _data![ idx ];
        return true;
    }

    public bool Remove( T manip )
    {
        var idx = _data?.BinarySearch( manip ) ?? -1;
        if( idx < 0 )
        {
            return false;
        }

        _data!.RemoveAt( idx );
        return true;
    }
}

[StructLayout( LayoutKind.Explicit, Pack = 1, Size = 16 )]
public readonly struct MetaManipulation : IEquatable< MetaManipulation >, IComparable< MetaManipulation >
{
    public enum Type : byte
    {
        Unknown = 0,
        Imc     = 1,
        Eqdp    = 2,
        Eqp     = 3,
        Est     = 4,
        Gmp     = 5,
        Rsp     = 6,
    }

    [FieldOffset( 0 )]
    [JsonIgnore]
    public readonly EqpManipulation Eqp = default;

    [FieldOffset( 0 )]
    [JsonIgnore]
    public readonly GmpManipulation Gmp = default;

    [FieldOffset( 0 )]
    [JsonIgnore]
    public readonly EqdpManipulation Eqdp = default;

    [FieldOffset( 0 )]
    [JsonIgnore]
    public readonly EstManipulation Est = default;

    [FieldOffset( 0 )]
    [JsonIgnore]
    public readonly RspManipulation Rsp = default;

    [FieldOffset( 0 )]
    [JsonIgnore]
    public readonly ImcManipulation Imc = default;

    [FieldOffset( 15 )]
    [JsonConverter( typeof( StringEnumConverter ) )]
    [JsonProperty("Type")]
    public readonly Type ManipulationType;

    public object? Manipulation
    {
        get => ManipulationType switch
        {
            Type.Unknown => null,
            Type.Imc     => Imc,
            Type.Eqdp    => Eqdp,
            Type.Eqp     => Eqp,
            Type.Est     => Est,
            Type.Gmp     => Gmp,
            Type.Rsp     => Rsp,
            _            => null,
        };
        init
        {
            switch( value )
            {
                case EqpManipulation m:
                    Eqp              = m;
                    ManipulationType = Type.Eqp;
                    return;
                case EqdpManipulation m:
                    Eqdp             = m;
                    ManipulationType = Type.Eqdp;
                    return;
                case GmpManipulation m:
                    Gmp              = m;
                    ManipulationType = Type.Gmp;
                    return;
                case EstManipulation m:
                    Est              = m;
                    ManipulationType = Type.Est;
                    return;
                case RspManipulation m:
                    Rsp              = m;
                    ManipulationType = Type.Rsp;
                    return;
                case ImcManipulation m:
                    Imc              = m;
                    ManipulationType = Type.Imc;
                    return;
            }
        }
    }

    public MetaManipulation( EqpManipulation eqp )
    {
        Eqp              = eqp;
        ManipulationType = Type.Eqp;
    }

    public MetaManipulation( GmpManipulation gmp )
    {
        Gmp              = gmp;
        ManipulationType = Type.Gmp;
    }

    public MetaManipulation( EqdpManipulation eqdp )
    {
        Eqdp             = eqdp;
        ManipulationType = Type.Eqdp;
    }

    public MetaManipulation( EstManipulation est )
    {
        Est              = est;
        ManipulationType = Type.Est;
    }

    public MetaManipulation( RspManipulation rsp )
    {
        Rsp              = rsp;
        ManipulationType = Type.Rsp;
    }

    public MetaManipulation( ImcManipulation imc )
    {
        Imc              = imc;
        ManipulationType = Type.Imc;
    }

    public static implicit operator MetaManipulation( EqpManipulation eqp )
        => new(eqp);

    public static implicit operator MetaManipulation( GmpManipulation gmp )
        => new(gmp);

    public static implicit operator MetaManipulation( EqdpManipulation eqdp )
        => new(eqdp);

    public static implicit operator MetaManipulation( EstManipulation est )
        => new(est);

    public static implicit operator MetaManipulation( RspManipulation rsp )
        => new(rsp);

    public static implicit operator MetaManipulation( ImcManipulation imc )
        => new(imc);

    public bool Equals( MetaManipulation other )
    {
        if( ManipulationType != other.ManipulationType )
        {
            return false;
        }

        return ManipulationType switch
        {
            Type.Eqp  => Eqp.Equals( other.Eqp ),
            Type.Gmp  => Gmp.Equals( other.Gmp ),
            Type.Eqdp => Eqdp.Equals( other.Eqdp ),
            Type.Est  => Est.Equals( other.Est ),
            Type.Rsp  => Rsp.Equals( other.Rsp ),
            Type.Imc  => Imc.Equals( other.Imc ),
            _         => throw new ArgumentOutOfRangeException(),
        };
    }

    public override bool Equals( object? obj )
        => obj is MetaManipulation other && Equals( other );

    public override int GetHashCode()
        => ManipulationType switch
        {
            Type.Eqp  => Eqp.GetHashCode(),
            Type.Gmp  => Gmp.GetHashCode(),
            Type.Eqdp => Eqdp.GetHashCode(),
            Type.Est  => Est.GetHashCode(),
            Type.Rsp  => Rsp.GetHashCode(),
            Type.Imc  => Imc.GetHashCode(),
            _         => throw new ArgumentOutOfRangeException(),
        };

    public unsafe int CompareTo( MetaManipulation other )
    {
        fixed( MetaManipulation* lhs = &this )
        {
            return Functions.MemCmpUnchecked( lhs, &other, sizeof( MetaManipulation ) );
        }
    }
}