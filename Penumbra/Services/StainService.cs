using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using OtterGui.Widgets;
using Penumbra.GameData.Data;
using Penumbra.GameData.Files;
using Penumbra.Util;

namespace Penumbra.Services;

public class StainService : IDisposable
{
    public sealed class StainTemplateCombo : FilterComboCache<ushort>
    {
        public StainTemplateCombo(IEnumerable<ushort> items)
            : base(items)
        { }
    }

    public readonly StainData StainData;
    public readonly FilterComboColors StainCombo;
    public readonly StmFile StmFile;
    public readonly StainTemplateCombo TemplateCombo;

    public StainService(StartTracker timer, DalamudPluginInterface pluginInterface, IDataManager dataManager)
    {
        using var t = timer.Measure(StartTimeType.Stains);
        StainData = new StainData(pluginInterface, dataManager, dataManager.Language);
        StainCombo = new FilterComboColors(140, StainData.Data.Prepend(new KeyValuePair<byte, (string Name, uint Dye, bool Gloss)>(0, ("None", 0, false))));
        StmFile = new StmFile(dataManager);
        TemplateCombo = new StainTemplateCombo(StmFile.Entries.Keys.Prepend((ushort)0));
        Penumbra.Log.Verbose($"[{nameof(StainService)}] Created.");
    }

    public void Dispose()
    {
        StainData.Dispose();
        Penumbra.Log.Verbose($"[{nameof(StainService)}] Disposed.");
    }
}