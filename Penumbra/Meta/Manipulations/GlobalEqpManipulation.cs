using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;

namespace Penumbra.Meta.Manipulations;

public readonly struct GlobalEqpManipulation : IMetaManipulation<GlobalEqpManipulation>
{
    public GlobalEqpType Type      { get; init; }
    public PrimaryId     Condition { get; init; }

    public bool Validate()
    {
        if (!Enum.IsDefined(Type))
            return false;

        if (Type is GlobalEqpType.DoNotHideVieraHats or GlobalEqpType.DoNotHideHrothgarHats)
            return Condition == 0;

        return Condition != 0;
    }


    public bool Equals(GlobalEqpManipulation other)
        => Type == other.Type
         && Condition.Equals(other.Condition);

    public int CompareTo(GlobalEqpManipulation other)
    {
        var typeComp = Type.CompareTo(other);
        return typeComp != 0 ? typeComp : Condition.Id.CompareTo(other.Condition.Id);
    }

    public override bool Equals(object? obj)
        => obj is GlobalEqpManipulation other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine((int)Type, Condition);

    public static bool operator ==(GlobalEqpManipulation left, GlobalEqpManipulation right)
        => left.Equals(right);

    public static bool operator !=(GlobalEqpManipulation left, GlobalEqpManipulation right)
        => !left.Equals(right);

    public override string ToString()
        => $"Global EQP - {Type}{(Condition != 0 ? $" - {Condition.Id}" : string.Empty)}";

    public MetaIndex FileIndex()
        => (MetaIndex)(-1);
}
