using System.Collections.Frozen;
using OtterGui.Services;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Mods.SubMods;

namespace Penumbra.Mods.Editor;

public class ModMetaEditor(
    ModGroupEditor groupEditor,
    MetaFileManager metaFileManager,
    ImcChecker imcChecker) : MetaDictionary, IService
{
    public sealed class OtherOptionData : HashSet<string>
    {
        public int TotalCount;

        public void Add(string name, int count)
        {
            if (count > 0)
                Add(name);
            TotalCount += count;
        }

        public new void Clear()
        {
            TotalCount = 0;
            base.Clear();
        }
    }

    public readonly FrozenDictionary<MetaManipulationType, OtherOptionData> OtherData =
        Enum.GetValues<MetaManipulationType>().ToFrozenDictionary(t => t, _ => new OtherOptionData());

    public bool Changes { get; set; }

    public new void Clear()
    {
        Changes = Count > 0;
        base.Clear();
    }

    public void Load(Mod mod, IModDataContainer currentOption)
    {
        foreach (var type in Enum.GetValues<MetaManipulationType>())
            OtherData[type].Clear();

        foreach (var option in mod.AllDataContainers)
        {
            if (option == currentOption)
                continue;

            var name = option.GetFullName();
            OtherData[MetaManipulationType.Imc].Add(name, option.Manipulations.GetCount(MetaManipulationType.Imc));
            OtherData[MetaManipulationType.Eqp].Add(name, option.Manipulations.GetCount(MetaManipulationType.Eqp));
            OtherData[MetaManipulationType.Eqdp].Add(name, option.Manipulations.GetCount(MetaManipulationType.Eqdp));
            OtherData[MetaManipulationType.Gmp].Add(name, option.Manipulations.GetCount(MetaManipulationType.Gmp));
            OtherData[MetaManipulationType.Est].Add(name, option.Manipulations.GetCount(MetaManipulationType.Est));
            OtherData[MetaManipulationType.Rsp].Add(name, option.Manipulations.GetCount(MetaManipulationType.Rsp));
            OtherData[MetaManipulationType.GlobalEqp].Add(name, option.Manipulations.GetCount(MetaManipulationType.GlobalEqp));
        }

        Clear();
        UnionWith(currentOption.Manipulations);
        Changes = false;
    }

    public static bool DeleteDefaultValues(MetaFileManager metaFileManager, ImcChecker imcChecker, MetaDictionary dict)
    {
        var clone = dict.Clone();
        dict.Clear();
        var count = 0;
        foreach (var (key, value) in clone.Imc)
        {
            var defaultEntry = imcChecker.GetDefaultEntry(key, false);
            if (!defaultEntry.Entry.Equals(value))
            {
                dict.TryAdd(key, value);
            }
            else
            {
                Penumbra.Log.Verbose($"Deleted default-valued meta-entry {key}.");
                ++count;
            }
        }

        foreach (var (key, value) in clone.Eqp)
        {
            var defaultEntry = new EqpEntryInternal(ExpandedEqpFile.GetDefault(metaFileManager, key.SetId), key.Slot);
            if (!defaultEntry.Equals(value))
            {
                dict.TryAdd(key, value);
            }
            else
            {
                Penumbra.Log.Verbose($"Deleted default-valued meta-entry {key}.");
                ++count;
            }
        }

        foreach (var (key, value) in clone.Eqdp)
        {
            var defaultEntry = new EqdpEntryInternal(ExpandedEqdpFile.GetDefault(metaFileManager, key), key.Slot);
            if (!defaultEntry.Equals(value))
            {
                dict.TryAdd(key, value);
            }
            else
            {
                Penumbra.Log.Verbose($"Deleted default-valued meta-entry {key}.");
                ++count;
            }
        }

        foreach (var (key, value) in clone.Est)
        {
            var defaultEntry = EstFile.GetDefault(metaFileManager, key);
            if (!defaultEntry.Equals(value))
            {
                dict.TryAdd(key, value);
            }
            else
            {
                Penumbra.Log.Verbose($"Deleted default-valued meta-entry {key}.");
                ++count;
            }
        }

        foreach (var (key, value) in clone.Gmp)
        {
            var defaultEntry = ExpandedGmpFile.GetDefault(metaFileManager, key);
            if (!defaultEntry.Equals(value))
            {
                dict.TryAdd(key, value);
            }
            else
            {
                Penumbra.Log.Verbose($"Deleted default-valued meta-entry {key}.");
                ++count;
            }
        }

        foreach (var (key, value) in clone.Rsp)
        {
            var defaultEntry = CmpFile.GetDefault(metaFileManager, key.SubRace, key.Attribute);
            if (!defaultEntry.Equals(value))
            {
                dict.TryAdd(key, value);
            }
            else
            {
                Penumbra.Log.Verbose($"Deleted default-valued meta-entry {key}.");
                ++count;
            }
        }

        if (count == 0)
            return false;

        Penumbra.Log.Debug($"Deleted {count} default-valued meta-entries from a mod option.");
        return true;
    }

    public void DeleteDefaultValues()
        => Changes = DeleteDefaultValues(metaFileManager, imcChecker, this);

    public void Apply(IModDataContainer container)
    {
        if (!Changes)
            return;

        groupEditor.SetManipulations(container, this);
        Changes = false;
    }
}
