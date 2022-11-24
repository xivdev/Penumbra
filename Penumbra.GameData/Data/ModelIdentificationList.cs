using System.Collections.Generic;
using Dalamud;
using Dalamud.Data;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Lumina.Excel.GeneratedSheets;
using Penumbra.GameData.Structs;

namespace Penumbra.GameData.Data;

internal sealed class ModelIdentificationList : KeyList<ModelChara>
{
    private const string Tag = "ModelIdentification";

    public ModelIdentificationList(DalamudPluginInterface pi, ClientLanguage language, DataManager gameData)
        : base(pi, Tag, language, ObjectIdentification.IdentificationVersion, CreateModelList(gameData, language))
    { }

    public IEnumerable<ModelChara> Between(CharacterBase.ModelType type, SetId modelId, byte modelBase = 0, byte variant = 0)
    {
        if (modelBase == 0)
            return Between(ToKey(type, modelId, 0, 0), ToKey(type, modelId, 0xFF, 0xFF));
        if (variant == 0)
            return Between(ToKey(type, modelId, modelBase, 0), ToKey(type, modelId, modelBase, 0xFF));

        return Between(ToKey(type, modelId, modelBase, variant), ToKey(type, modelId, modelBase, variant));
    }

    public void Dispose(DalamudPluginInterface pi, ClientLanguage language)
        => DataSharer.DisposeTag(pi, Tag, language, ObjectIdentification.IdentificationVersion);


    public static ulong ToKey(CharacterBase.ModelType type, SetId model, byte modelBase, byte variant)
        => ((ulong)type << 32) | ((ulong)model << 16) | ((ulong)modelBase << 8) | variant;

    private static ulong ToKey(ModelChara row)
        => ToKey((CharacterBase.ModelType)row.Type, row.Model, row.Base, row.Variant);

    protected override IEnumerable<ulong> ToKeys(ModelChara row)
    {
        yield return ToKey(row);
    }

    protected override bool ValidKey(ulong key)
        => key != 0;

    protected override int ValueKeySelector(ModelChara data)
        => (int)data.RowId;

    private static IEnumerable<ModelChara> CreateModelList(DataManager gameData, ClientLanguage language)
        => gameData.GetExcelSheet<ModelChara>(language)!;
}
