using ImSharp;
using Luna;
using Penumbra.Mods.Editor;

namespace Penumbra.Mods.Manager;

public class ModCombo(ModStorage modStorage) : SimpleFilterCombo<Mod>(SimpleFilterType.Regex), IUiService
{
    protected readonly ModStorage ModStorage = modStorage;

    public override StringU8 DisplayString(in Mod value)
        => new(value.Name);

    public override string FilterString(in Mod value)
        => value.Name;

    public override IEnumerable<Mod> GetBaseItems()
        => ModStorage;
}

public sealed class ModComboWithoutCurrent(ModStorage modStorage, ModMerger modMerger) : ModCombo(modStorage)
{
    public override IEnumerable<Mod> GetBaseItems()
        => ModStorage.Where(m => m != modMerger.MergeFromMod);
}
