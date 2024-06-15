using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public sealed class EqpCache(MetaFileManager manager, ModCollection collection) : MetaCacheBase<EqpIdentifier, EqpEntry>(manager, collection)
{
    private ExpandedEqpFile? _eqpFile;

    public override void SetFiles()
        => Manager.SetFile(_eqpFile, MetaIndex.Eqp);

    public override void ResetFiles()
        => Manager.SetFile(null, MetaIndex.Eqp);

    protected override void IncorporateChangesInternal()
    {
        if (GetFile() is not { } file)
            return;

        foreach (var (identifier, (_, entry)) in this)
            Apply(file, identifier, entry);

        Penumbra.Log.Verbose($"{Collection.AnonymizedName}: Loaded {Count} delayed EQP manipulations.");
    }

    public unsafe EqpEntry GetValues(CharacterArmor* armor)
        => GetSingleValue(armor[0].Set,  EquipSlot.Head)
          | GetSingleValue(armor[1].Set, EquipSlot.Body)
          | GetSingleValue(armor[2].Set, EquipSlot.Hands)
          | GetSingleValue(armor[3].Set, EquipSlot.Legs)
          | GetSingleValue(armor[4].Set, EquipSlot.Feet);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private EqpEntry GetSingleValue(PrimaryId id, EquipSlot slot)
        => TryGetValue(new EqpIdentifier(id, slot), out var pair) ? pair.Entry : ExpandedEqpFile.GetDefault(manager, id) & Eqp.Mask(slot);

    public MetaList.MetaReverter TemporarilySetFile()
        => Manager.TemporarilySetFile(_eqpFile, MetaIndex.Eqp);

    public override void Reset()
    {
        if (_eqpFile == null)
            return;

        _eqpFile.Reset(Keys.Select(identifier => identifier.SetId));
        Clear();
    }

    protected override void ApplyModInternal(EqpIdentifier identifier, EqpEntry entry)
    {
        if (GetFile() is { } file)
            Apply(file, identifier, entry);
    }

    protected override void RevertModInternal(EqpIdentifier identifier)
    {
        if (GetFile() is { } file)
            Apply(file, identifier, ExpandedEqpFile.GetDefault(Manager, identifier.SetId));
    }

    public static bool Apply(ExpandedEqpFile file, EqpIdentifier identifier, EqpEntry entry)
    {
        var origEntry = file[identifier.SetId];
        var mask      = Eqp.Mask(identifier.Slot);
        if ((origEntry & mask) == entry)
            return false;

        file[identifier.SetId] = (origEntry & ~mask) | entry;
        return true;
    }

    protected override void Dispose(bool _)
    {
        _eqpFile?.Dispose();
        _eqpFile = null;
        Clear();
    }

    private ExpandedEqpFile? GetFile()
    {
        if (_eqpFile != null)
            return _eqpFile;

        if (!Manager.CharacterUtility.Ready)
            return null;

        return _eqpFile = new ExpandedEqpFile(Manager);
    }
}
