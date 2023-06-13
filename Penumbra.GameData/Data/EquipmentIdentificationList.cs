using System.Collections.Generic;
using System.Linq;
using Dalamud;
using Dalamud.Data;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using PseudoEquipItem = System.ValueTuple<string, uint, ushort, ushort, ushort, byte, byte>;

namespace Penumbra.GameData.Data;

internal sealed class EquipmentIdentificationList : KeyList<PseudoEquipItem>
{
    private const string Tag = "EquipmentIdentification";

    public EquipmentIdentificationList(DalamudPluginInterface pi, ClientLanguage language, DataManager gameData)
        : base(pi, Tag, language, ObjectIdentification.IdentificationVersion, CreateEquipmentList(gameData, language))
    { }

    public IEnumerable<EquipItem> Between(SetId modelId, EquipSlot slot = EquipSlot.Unknown, byte variant = 0)
    {
        if (slot == EquipSlot.Unknown)
            return Between(ToKey(modelId, 0, 0), ToKey(modelId, (EquipSlot)0xFF, 0xFF)).Select(e => (EquipItem)e);
        if (variant == 0)
            return Between(ToKey(modelId, slot, 0), ToKey(modelId, slot, 0xFF)).Select(e => (EquipItem)e);

        return Between(ToKey(modelId, slot, variant), ToKey(modelId, slot, variant)).Select(e => (EquipItem)e);
    }

    public void Dispose(DalamudPluginInterface pi, ClientLanguage language)
        => DataSharer.DisposeTag(pi, Tag, language, ObjectIdentification.IdentificationVersion);

    public static ulong ToKey(SetId modelId, EquipSlot slot, byte variant)
        => ((ulong)modelId << 32) | ((ulong)slot << 16) | variant;

    public static ulong ToKey(EquipItem i)
        => ToKey(i.ModelId, i.Type.ToSlot(), i.Variant);

    protected override IEnumerable<ulong> ToKeys(PseudoEquipItem i)
    {
        yield return ToKey(i);
    }

    protected override bool ValidKey(ulong key)
        => key != 0;

    protected override int ValueKeySelector(PseudoEquipItem data)
        => (int)data.Item2;

    private static IEnumerable<PseudoEquipItem> CreateEquipmentList(DataManager gameData, ClientLanguage language)
    {
        var items = gameData.GetExcelSheet<Item>(language)!;
        return items.Where(i => ((EquipSlot)i.EquipSlotCategory.Row).IsEquipmentPiece()).Select(i => (PseudoEquipItem)EquipItem.FromArmor(i));
    }
}
