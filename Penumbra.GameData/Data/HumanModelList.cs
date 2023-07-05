using System.Collections;
using System.Linq;
using Dalamud;
using Dalamud.Data;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Lumina.Excel.GeneratedSheets;

namespace Penumbra.GameData.Data;

public sealed class HumanModelList : DataSharer
{
    public const string Tag     = "HumanModels";
    public const int    CurrentVersion = 1;

    private readonly BitArray _humanModels;

    public HumanModelList(DalamudPluginInterface pluginInterface, DataManager gameData)
        : base(pluginInterface, ClientLanguage.English, CurrentVersion)
    {
        _humanModels = TryCatchData(Tag, () => GetValidHumanModels(gameData));
    }

    public bool IsHuman(uint modelId)
        => modelId < _humanModels.Count && _humanModels[(int)modelId];

    protected override void DisposeInternal()
    {
        DisposeTag(Tag);
    }

    /// <summary>
    /// Go through all ModelChara rows and return a bitfield of those that resolve to human models.
    /// </summary>
    private static BitArray GetValidHumanModels(DataManager gameData)
    {
        var sheet = gameData.GetExcelSheet<ModelChara>()!;
        var ret   = new BitArray((int)sheet.RowCount, false);
        foreach (var (_, idx) in sheet.Select((m, i) => (m, i)).Where(p => p.m.Type == (byte)CharacterBase.ModelType.Human))
            ret[idx] = true;

        return ret;
    }
}
