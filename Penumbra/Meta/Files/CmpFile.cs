using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;
using Penumbra.Interop.Services;
using Penumbra.String.Functions;

namespace Penumbra.Meta.Files;

/// <summary>
/// The human.cmp file contains many character-relevant parameters like color sets.
/// We only support manipulating the racial scaling parameters at the moment.
/// </summary>
public sealed unsafe class CmpFile : MetaBaseFile
{
    public static readonly CharacterUtility.InternalIndex InternalIndex =
        CharacterUtility.ReverseIndices[(int)MetaIndex.HumanCmp];

    private const int RacialScalingStart = 0x2A800;

    public float this[SubRace subRace, RspAttribute attribute]
    {
        get => *(float*)(Data + RacialScalingStart + ToRspIndex(subRace) * RspEntry.ByteSize + (int)attribute * 4);
        set => *(float*)(Data + RacialScalingStart + ToRspIndex(subRace) * RspEntry.ByteSize + (int)attribute * 4) = value;
    }

    public override void Reset()
        => MemoryUtility.MemCpyUnchecked(Data, (byte*)DefaultData.Data, DefaultData.Length);

    public void Reset(IEnumerable<(SubRace, RspAttribute)> entries)
    {
        foreach (var (r, a) in entries)
            this[r, a] = GetDefault(Manager, r, a);
    }

    public CmpFile(MetaFileManager manager)
        : base(manager, MetaIndex.HumanCmp)
    {
        AllocateData(DefaultData.Length);
        Reset();
    }

    public static float GetDefault(MetaFileManager manager, SubRace subRace, RspAttribute attribute)
    {
        var data = (byte*)manager.CharacterUtility.DefaultResource(InternalIndex).Address;
        return *(float*)(data + RacialScalingStart + ToRspIndex(subRace) * RspEntry.ByteSize + (int)attribute * 4);
    }

    private static int ToRspIndex(SubRace subRace)
        => subRace switch
        {
            SubRace.Midlander       => 0,
            SubRace.Highlander      => 1,
            SubRace.Wildwood        => 10,
            SubRace.Duskwight       => 11,
            SubRace.Plainsfolk      => 20,
            SubRace.Dunesfolk       => 21,
            SubRace.SeekerOfTheSun  => 30,
            SubRace.KeeperOfTheMoon => 31,
            SubRace.Seawolf         => 40,
            SubRace.Hellsguard      => 41,
            SubRace.Raen            => 50,
            SubRace.Xaela           => 51,
            SubRace.Helion          => 60,
            SubRace.Lost            => 61,
            SubRace.Rava            => 70,
            SubRace.Veena           => 71,
            SubRace.Unknown         => 0,
            _                       => throw new ArgumentOutOfRangeException(nameof(subRace), subRace, null),
        };
}
