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
        Hair = MetaIndex.HairEst,
        Face = MetaIndex.FaceEst,
        Body = MetaIndex.BodyEst,
        Head = MetaIndex.HeadEst,
    }

    public static string ToName( EstType type )
        => type switch
        {
            EstType.Hair => "hair",
            EstType.Face => "face",
            EstType.Body => "top",
            EstType.Head => "met",
            _            => "unk",
        };

    public ushort Entry { get; private init; } // SkeletonIdx.

    [JsonConverter( typeof( StringEnumConverter ) )]
    public Gender Gender { get; private init; }

    [JsonConverter( typeof( StringEnumConverter ) )]
    public ModelRace Race { get; private init; }

    public ushort SetId { get; private init; }

    [JsonConverter( typeof( StringEnumConverter ) )]
    public EstType Slot { get; private init; }

    [JsonConstructor]
    public EstManipulation( Gender gender, ModelRace race, EstType slot, ushort setId, ushort entry )
    {
        Entry  = entry;
        Gender = gender;
        Race   = race;
        SetId  = setId;
        Slot   = slot;
    }

    public EstManipulation Copy( ushort entry )
        => new(Gender, Race, Slot, SetId, entry);


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

    public MetaIndex FileIndex()
        => ( MetaIndex )Slot;

    public bool Apply( EstFile file )
    {
        return file.SetEntry( Names.CombinedRace( Gender, Race ), SetId, Entry ) switch
        {
            EstFile.EstEntryChange.Unchanged => false,
            EstFile.EstEntryChange.Changed   => true,
            EstFile.EstEntryChange.Added     => true,
            EstFile.EstEntryChange.Removed   => true,
            _                                => throw new ArgumentOutOfRangeException(),
        };
    }
}