using System;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;

namespace Penumbra.Meta.Manipulations;

public readonly struct EstManipulation : IEquatable< EstManipulation >
{
    public enum EstType : byte
    {
        Hair = CharacterUtility.HairEstIdx,
        Face = CharacterUtility.FaceEstIdx,
        Body = CharacterUtility.BodyEstIdx,
        Head = CharacterUtility.HeadEstIdx,
    }

    public readonly ushort    SkeletonIdx;
    public readonly Gender    Gender;
    public readonly ModelRace Race;
    public readonly ushort    SetId;
    public readonly EstType   Type;

    public EstManipulation( Gender gender, ModelRace race, EstType estType, ushort setId, ushort skeletonIdx )
    {
        SkeletonIdx = skeletonIdx;
        Gender      = gender;
        Race        = race;
        SetId       = setId;
        Type        = estType;
    }


    public override string ToString()
        => $"Est - {SetId} - {Type} - {Race.ToName()} {Gender.ToName()}";

    public bool Equals( EstManipulation other )
        => Gender == other.Gender
         && Race  == other.Race
         && SetId == other.SetId
         && Type  == other.Type;

    public override bool Equals( object? obj )
        => obj is EstManipulation other && Equals( other );

    public override int GetHashCode()
        => HashCode.Combine( ( int )Gender, ( int )Race, SetId, ( int )Type );

    public int FileIndex()
        => ( int )Type;

    public bool Apply( EstFile file )
    {
        return file.SetEntry( Names.CombinedRace( Gender, Race ), SetId, SkeletonIdx ) switch
        {
            EstFile.EstEntryChange.Unchanged => false,
            EstFile.EstEntryChange.Changed   => true,
            EstFile.EstEntryChange.Added     => true,
            EstFile.EstEntryChange.Removed   => true,
            _                                => throw new ArgumentOutOfRangeException(),
        };
    }
}