using System.Collections.Generic;
using System.Linq;
using Dalamud;
using Dalamud.Data;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

namespace Penumbra.GameData.Data;

internal sealed class EquipmentIdentificationList : KeyList<Item>
{
    private const string Tag     = "EquipmentIdentification";

    public EquipmentIdentificationList(DalamudPluginInterface pi, ClientLanguage language, DataManager gameData)
        : base(pi, Tag, language, ObjectIdentification.IdentificationVersion, CreateEquipmentList(gameData, language))
    { }

    public IEnumerable<Item> Between(SetId modelId, EquipSlot slot = EquipSlot.Unknown, byte variant = 0)
    {
        if (slot == EquipSlot.Unknown)
            return Between(ToKey(modelId, 0, 0), ToKey(modelId, (EquipSlot)0xFF, 0xFF));
        if (variant == 0)
            return Between(ToKey(modelId, slot, 0), ToKey(modelId, slot, 0xFF));

        return Between(ToKey(modelId, slot, variant), ToKey(modelId, slot, variant));
    }

    public void Dispose(DalamudPluginInterface pi, ClientLanguage language)
        => DataSharer.DisposeTag(pi, Tag, language, ObjectIdentification.IdentificationVersion);

    public static ulong ToKey(SetId modelId, EquipSlot slot, byte variant)
        => ((ulong)modelId << 32) | ((ulong)slot << 16) | variant;

    public static ulong ToKey(Item i)
    {
        var model   = (SetId)((Lumina.Data.Parsing.Quad)i.ModelMain).A;
        var slot    = ((EquipSlot)i.EquipSlotCategory.Row).ToSlot();
        var variant = (byte)((Lumina.Data.Parsing.Quad)i.ModelMain).B;
        return ToKey(model, slot, variant);
    }

    protected override IEnumerable<ulong> ToKeys(Item i)
    {
        yield return ToKey(i);
    }

    protected override bool ValidKey(ulong key)
        => key != 0;

    protected override int ValueKeySelector(Item data)
        => (int)data.RowId;

    private static IEnumerable<Item> CreateEquipmentList(DataManager gameData, ClientLanguage language)
    {
        var items = gameData.GetExcelSheet<Item>(language)!;
        return items.Where(i => ((EquipSlot)i.EquipSlotCategory.Row).IsEquipmentPiece());
    }
}
