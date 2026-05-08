using Penumbra.GameData.Enums;
using Penumbra.GameData.Files;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Manipulations;
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

    public new CmpData* Data
        => (CmpData*)base.Data;

    public RspEntry this[SubRace subRace, RspAttribute attribute]
    {
        get => new(Data->GetScaleWrite(subRace).Get(attribute));
        set => Data->GetScaleWrite(subRace).Get(attribute) = value.Value;
    }

    public override void Reset()
        => MemoryUtility.MemCpyUnchecked(Data, (byte*)DefaultData.Data, DefaultData.Length);

    public void Reset(IEnumerable<(SubRace, RspAttribute)> entries)
    {
        foreach (var (r, a) in entries)
            this[r, a] = GetDefault(Manager, r, a);
    }

    public CmpFile(MetaFileManager manager)
        : base(manager, manager.MarshalAllocator, MetaIndex.HumanCmp)
    {
        AllocateData(DefaultData.Length);
        Reset();
    }

    public static RspEntry GetDefault(MetaFileManager manager, SubRace subRace, RspAttribute attribute)
    {
        var data = (CmpData*)manager.CharacterUtility.DefaultResource(InternalIndex).Address;
        return new RspEntry(data->GetScaleWrite(subRace).Get(attribute));
    }

    public static RspEntry* GetDefaults(MetaFileManager manager, SubRace subRace, RspAttribute attribute)
    {
        {
            var data = (CmpData*)manager.CharacterUtility.DefaultResource(InternalIndex).Address;
            return (RspEntry*)Unsafe.AsPointer(ref data->GetScaleWrite(subRace).Get(attribute));
        }
    }
}
