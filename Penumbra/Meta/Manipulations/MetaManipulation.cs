using System;
using System.Runtime.InteropServices;

namespace Penumbra.Meta.Manipulations;

[StructLayout( LayoutKind.Explicit, Pack = 1, Size = 16 )]
public readonly struct MetaManipulation : IEquatable< MetaManipulation >
{
    public enum Type : byte
    {
        Eqp,
        Gmp,
        Eqdp,
        Est,
        Rsp,
        Imc,
    }

    [FieldOffset( 0 )]
    public readonly EqpManipulation Eqp = default;

    [FieldOffset( 0 )]
    public readonly GmpManipulation Gmp = default;

    [FieldOffset( 0 )]
    public readonly EqdpManipulation Eqdp = default;

    [FieldOffset( 0 )]
    public readonly EstManipulation Est = default;

    [FieldOffset( 0 )]
    public readonly RspManipulation Rsp = default;

    [FieldOffset( 0 )]
    public readonly ImcManipulation Imc = default;

    [FieldOffset( 15 )]
    public readonly Type ManipulationType;

    public MetaManipulation( EqpManipulation eqp )
        => ( ManipulationType, Eqp ) = ( Type.Eqp, eqp );

    public MetaManipulation( GmpManipulation gmp )
        => ( ManipulationType, Gmp ) = ( Type.Gmp, gmp );

    public MetaManipulation( EqdpManipulation eqdp )
        => ( ManipulationType, Eqdp ) = ( Type.Eqdp, eqdp );

    public MetaManipulation( EstManipulation est )
        => ( ManipulationType, Est ) = ( Type.Est, est );

    public MetaManipulation( RspManipulation rsp )
        => ( ManipulationType, Rsp ) = ( Type.Rsp, rsp );

    public MetaManipulation( ImcManipulation imc )
        => ( ManipulationType, Imc ) = ( Type.Imc, imc );

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
}