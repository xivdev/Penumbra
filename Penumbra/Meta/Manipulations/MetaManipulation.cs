using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Penumbra.Interop.Structs;
using Penumbra.String.Functions;

namespace Penumbra.Meta.Manipulations;

public interface IMetaManipulation
{
    public MetaIndex FileIndex();
}

public interface IMetaManipulation<T>
    : IMetaManipulation, IComparable<T>, IEquatable<T> where T : struct
{ }

[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 16)]
public readonly struct MetaManipulation : IEquatable<MetaManipulation>, IComparable<MetaManipulation>
{
    public const int CurrentVersion = 0;

    public enum Type : byte
    {
        Unknown   = 0,
        Imc       = 1,
        Eqdp      = 2,
        Eqp       = 3,
        Est       = 4,
        Gmp       = 5,
        Rsp       = 6,
        GlobalEqp = 7,
    }

    [FieldOffset(0)]
    [JsonIgnore]
    public readonly EqpManipulation Eqp = default;

    [FieldOffset(0)]
    [JsonIgnore]
    public readonly GmpManipulation Gmp = default;

    [FieldOffset(0)]
    [JsonIgnore]
    public readonly EqdpManipulation Eqdp = default;

    [FieldOffset(0)]
    [JsonIgnore]
    public readonly EstManipulation Est = default;

    [FieldOffset(0)]
    [JsonIgnore]
    public readonly RspManipulation Rsp = default;

    [FieldOffset(0)]
    [JsonIgnore]
    public readonly ImcManipulation Imc = default;

    [FieldOffset(0)]
    [JsonIgnore]
    public readonly GlobalEqpManipulation GlobalEqp = default;

    [FieldOffset(15)]
    [JsonConverter(typeof(StringEnumConverter))]
    [JsonProperty("Type")]
    public readonly Type ManipulationType;

    public object? Manipulation
    {
        get => ManipulationType switch
        {
            Type.Unknown   => null,
            Type.Imc       => Imc,
            Type.Eqdp      => Eqdp,
            Type.Eqp       => Eqp,
            Type.Est       => Est,
            Type.Gmp       => Gmp,
            Type.Rsp       => Rsp,
            Type.GlobalEqp => GlobalEqp,
            _              => null,
        };
        init
        {
            switch (value)
            {
                case EqpManipulation m:
                    Eqp              = m;
                    ManipulationType = m.Validate() ? Type.Eqp : Type.Unknown;
                    return;
                case EqdpManipulation m:
                    Eqdp             = m;
                    ManipulationType = m.Validate() ? Type.Eqdp : Type.Unknown;
                    return;
                case GmpManipulation m:
                    Gmp              = m;
                    ManipulationType = m.Validate() ? Type.Gmp : Type.Unknown;
                    return;
                case EstManipulation m:
                    Est              = m;
                    ManipulationType = m.Validate() ? Type.Est : Type.Unknown;
                    return;
                case RspManipulation m:
                    Rsp              = m;
                    ManipulationType = m.Validate() ? Type.Rsp : Type.Unknown;
                    return;
                case ImcManipulation m:
                    Imc              = m;
                    ManipulationType = m.Validate(true) ? Type.Imc : Type.Unknown;
                    return;
                case GlobalEqpManipulation m:
                    GlobalEqp        = m;
                    ManipulationType = m.Validate() ? Type.GlobalEqp : Type.Unknown;
                    return;
            }
        }
    }

    public bool Validate()
    {
        return ManipulationType switch
        {
            Type.Imc       => Imc.Validate(true),
            Type.Eqdp      => Eqdp.Validate(),
            Type.Eqp       => Eqp.Validate(),
            Type.Est       => Est.Validate(),
            Type.Gmp       => Gmp.Validate(),
            Type.Rsp       => Rsp.Validate(),
            Type.GlobalEqp => GlobalEqp.Validate(),
            _              => false,
        };
    }

    public MetaManipulation(EqpManipulation eqp)
    {
        Eqp              = eqp;
        ManipulationType = Type.Eqp;
    }

    public MetaManipulation(GmpManipulation gmp)
    {
        Gmp              = gmp;
        ManipulationType = Type.Gmp;
    }

    public MetaManipulation(EqdpManipulation eqdp)
    {
        Eqdp             = eqdp;
        ManipulationType = Type.Eqdp;
    }

    public MetaManipulation(EstManipulation est)
    {
        Est              = est;
        ManipulationType = Type.Est;
    }

    public MetaManipulation(RspManipulation rsp)
    {
        Rsp              = rsp;
        ManipulationType = Type.Rsp;
    }

    public MetaManipulation(ImcManipulation imc)
    {
        Imc              = imc;
        ManipulationType = Type.Imc;
    }

    public MetaManipulation(GlobalEqpManipulation eqp)
    {
        GlobalEqp        = eqp;
        ManipulationType = Type.GlobalEqp;
    }

    public static implicit operator MetaManipulation(EqpManipulation eqp)
        => new(eqp);

    public static implicit operator MetaManipulation(GmpManipulation gmp)
        => new(gmp);

    public static implicit operator MetaManipulation(EqdpManipulation eqdp)
        => new(eqdp);

    public static implicit operator MetaManipulation(EstManipulation est)
        => new(est);

    public static implicit operator MetaManipulation(RspManipulation rsp)
        => new(rsp);

    public static implicit operator MetaManipulation(ImcManipulation imc)
        => new(imc);

    public static implicit operator MetaManipulation(GlobalEqpManipulation eqp)
        => new(eqp);

    public bool EntryEquals(MetaManipulation other)
    {
        if (ManipulationType != other.ManipulationType)
            return false;

        return ManipulationType switch
        {
            Type.Eqp       => Eqp.Entry.Equals(other.Eqp.Entry),
            Type.Gmp       => Gmp.Entry.Equals(other.Gmp.Entry),
            Type.Eqdp      => Eqdp.Entry.Equals(other.Eqdp.Entry),
            Type.Est       => Est.Entry.Equals(other.Est.Entry),
            Type.Rsp       => Rsp.Entry.Equals(other.Rsp.Entry),
            Type.Imc       => Imc.Entry.Equals(other.Imc.Entry),
            Type.GlobalEqp => true,
            _              => throw new ArgumentOutOfRangeException(),
        };
    }

    public bool Equals(MetaManipulation other)
    {
        if (ManipulationType != other.ManipulationType)
            return false;

        return ManipulationType switch
        {
            Type.Eqp       => Eqp.Equals(other.Eqp),
            Type.Gmp       => Gmp.Equals(other.Gmp),
            Type.Eqdp      => Eqdp.Equals(other.Eqdp),
            Type.Est       => Est.Equals(other.Est),
            Type.Rsp       => Rsp.Equals(other.Rsp),
            Type.Imc       => Imc.Equals(other.Imc),
            Type.GlobalEqp => GlobalEqp.Equals(other.GlobalEqp),
            _              => false,
        };
    }

    public MetaManipulation WithEntryOf(MetaManipulation other)
    {
        if (ManipulationType != other.ManipulationType)
            return this;

        return ManipulationType switch
        {
            Type.Eqp       => Eqp.Copy(other.Eqp.Entry),
            Type.Gmp       => Gmp.Copy(other.Gmp.Entry),
            Type.Eqdp      => Eqdp.Copy(other.Eqdp),
            Type.Est       => Est.Copy(other.Est.Entry),
            Type.Rsp       => Rsp.Copy(other.Rsp.Entry),
            Type.Imc       => Imc.Copy(other.Imc.Entry),
            Type.GlobalEqp => GlobalEqp,
            _              => throw new ArgumentOutOfRangeException(),
        };
    }

    public override bool Equals(object? obj)
        => obj is MetaManipulation other && Equals(other);

    public override int GetHashCode()
        => ManipulationType switch
        {
            Type.Eqp       => Eqp.GetHashCode(),
            Type.Gmp       => Gmp.GetHashCode(),
            Type.Eqdp      => Eqdp.GetHashCode(),
            Type.Est       => Est.GetHashCode(),
            Type.Rsp       => Rsp.GetHashCode(),
            Type.Imc       => Imc.GetHashCode(),
            Type.GlobalEqp => GlobalEqp.GetHashCode(),
            _              => 0,
        };

    public unsafe int CompareTo(MetaManipulation other)
    {
        fixed (MetaManipulation* lhs = &this)
        {
            return MemoryUtility.MemCmpUnchecked(lhs, &other, sizeof(MetaManipulation));
        }
    }

    public override string ToString()
        => ManipulationType switch
        {
            Type.Eqp       => Eqp.ToString(),
            Type.Gmp       => Gmp.ToString(),
            Type.Eqdp      => Eqdp.ToString(),
            Type.Est       => Est.ToString(),
            Type.Rsp       => Rsp.ToString(),
            Type.Imc       => Imc.ToString(),
            Type.GlobalEqp => GlobalEqp.ToString(),
            _              => "Invalid",
        };

    public string EntryToString()
        => ManipulationType switch
        {
            Type.Imc =>
                $"{Imc.Entry.DecalId}-{Imc.Entry.MaterialId}-{Imc.Entry.VfxId}-{Imc.Entry.SoundId}-{Imc.Entry.MaterialAnimationId}-{Imc.Entry.AttributeMask}",
            Type.Eqdp      => $"{(ushort)Eqdp.Entry:X}",
            Type.Eqp       => $"{(ulong)Eqp.Entry:X}",
            Type.Est       => $"{Est.Entry}",
            Type.Gmp       => $"{Gmp.Entry.Value}",
            Type.Rsp       => $"{Rsp.Entry}",
            Type.GlobalEqp => string.Empty,
            _              => string.Empty,
        };

    public static bool operator ==(MetaManipulation left, MetaManipulation right)
        => left.Equals(right);

    public static bool operator !=(MetaManipulation left, MetaManipulation right)
        => !(left == right);

    public static bool operator <(MetaManipulation left, MetaManipulation right)
        => left.CompareTo(right) < 0;

    public static bool operator <=(MetaManipulation left, MetaManipulation right)
        => left.CompareTo(right) <= 0;

    public static bool operator >(MetaManipulation left, MetaManipulation right)
        => left.CompareTo(right) > 0;

    public static bool operator >=(MetaManipulation left, MetaManipulation right)
        => left.CompareTo(right) >= 0;
}
