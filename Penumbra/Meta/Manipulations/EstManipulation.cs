using System;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;

namespace Penumbra.Meta.Manipulations;

[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public readonly struct EstManipulation : IMetaManipulation< EstManipulation >
{
    public enum EstType : byte
    {
        Hair = CharacterUtility.HairEstIdx,
        Face = CharacterUtility.FaceEstIdx,
        Body = CharacterUtility.BodyEstIdx,
        Head = CharacterUtility.HeadEstIdx,
    }

    public readonly ushort    SkeletonIdx;
    [JsonConverter( typeof( StringEnumConverter ) )]
    public readonly Gender    Gender;
    [JsonConverter( typeof( StringEnumConverter ) )]
    public readonly ModelRace Race;
    public readonly ushort    SetId;
    [JsonConverter( typeof( StringEnumConverter ) )]
    public readonly EstType   Slot;

    public EstManipulation( Gender gender, ModelRace race, EstType estType, ushort setId, ushort skeletonIdx )
    {
        SkeletonIdx = skeletonIdx;
        Gender      = gender;
        Race        = race;
        SetId       = setId;
        Slot        = estType;
    }


    public override string ToString()
        => $"Est - {SetId} - {Slot} - {Race.ToName()} {Gender.ToName()}";

    public bool Equals( EstManipulation other )
        => Gender == other.Gender
         && Race  == other.Race
         && SetId == other.SetId
         && Slot  == other.Slot;

    public override bool Equals( object? obj )
        => obj is EstManipulation other && Equals( other );

    public override int GetHashCode()
        => HashCode.Combine( ( int )Gender, ( int )Race, SetId, ( int )Slot );

    public int CompareTo( EstManipulation other )
    {
        var r = Race.CompareTo( other.Race );
        if( r != 0 )
        {
            return r;
        }

        var g = Gender.CompareTo( other.Gender );
        if( g != 0 )
        {
            return g;
        }

        var s = Slot.CompareTo( other.Slot );
        return s != 0 ? s : SetId.CompareTo( other.SetId );
    }

    public int FileIndex()
        => ( int )Slot;

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