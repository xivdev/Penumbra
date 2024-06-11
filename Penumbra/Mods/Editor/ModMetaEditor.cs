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

    public readonly FrozenDictionary<MetaManipulation.Type, OtherOptionData> OtherData =
        Enum.GetValues<MetaManipulation.Type>().ToFrozenDictionary(t => t, _ => new OtherOptionData());

    public bool Changes { get; private set; }

    public new void Clear()
    {
        base.Clear();
        Changes = true;
    }

    public void Load(Mod mod, IModDataContainer currentOption)
    {
        foreach (var type in Enum.GetValues<MetaManipulation.Type>())
            OtherData[type].Clear();

        foreach (var option in mod.AllDataContainers)
        {
            if (option == currentOption)
                continue;

            var name = option.GetFullName();
            OtherData[MetaManipulation.Type.Imc].Add(name, option.Manipulations.GetCount(MetaManipulation.Type.Imc));
            OtherData[MetaManipulation.Type.Eqp].Add(name, option.Manipulations.GetCount(MetaManipulation.Type.Eqp));
            OtherData[MetaManipulation.Type.Eqdp].Add(name, option.Manipulations.GetCount(MetaManipulation.Type.Eqdp));
            OtherData[MetaManipulation.Type.Gmp].Add(name, option.Manipulations.GetCount(MetaManipulation.Type.Gmp));
            OtherData[MetaManipulation.Type.Est].Add(name, option.Manipulations.GetCount(MetaManipulation.Type.Est));
            OtherData[MetaManipulation.Type.Rsp].Add(name, option.Manipulations.GetCount(MetaManipulation.Type.Rsp));
            OtherData[MetaManipulation.Type.GlobalEqp].Add(name, option.Manipulations.GetCount(MetaManipulation.Type.GlobalEqp));
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
