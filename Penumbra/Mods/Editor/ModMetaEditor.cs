using System.Collections.Frozen;
using OtterGui.Services;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Manager;
using Penumbra.Mods.SubMods;

namespace Penumbra.Mods.Editor;

public class ModMetaEditor(ModManager modManager) : MetaDictionary, IService
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

    public void Apply(IModDataContainer container)
    {
        if (!Changes)
            return;

        modManager.OptionEditor.SetManipulations(container, this);
        Changes = false;
    }
}
