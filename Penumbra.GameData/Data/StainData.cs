using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dalamud;
using Dalamud.Data;
using Dalamud.Plugin;
using Penumbra.GameData.Structs;

namespace Penumbra.GameData.Data;

public sealed class StainData : DataSharer, IReadOnlyDictionary<StainId, Stain>
{
    public readonly IReadOnlyDictionary<byte, (string Name, uint Dye, bool Gloss)> Data;

    public StainData(DalamudPluginInterface pluginInterface, DataManager dataManager, ClientLanguage language)
        : base(pluginInterface, language, 1)
    {
        Data = TryCatchData("Stains", () => CreateStainData(dataManager));
    }

    protected override void DisposeInternal()
        => DisposeTag("Stains");

    private IReadOnlyDictionary<byte, (string Name, uint Dye, bool Gloss)> CreateStainData(DataManager dataManager)
    {
        var stainSheet = dataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.Stain>(Language)!;
        return stainSheet.Where(s => s.Color != 0 && s.Name.RawData.Length > 0)
            .ToDictionary(s => (byte)s.RowId, s =>
            {
                var stain = new Stain(s);
                return (stain.Name, stain.RgbaColor, stain.Gloss);
            });
    }

    public IEnumerator<KeyValuePair<StainId, Stain>> GetEnumerator()
        => Data.Select(kvp
                => new KeyValuePair<StainId, Stain>(new StainId(kvp.Key), new Stain(kvp.Value.Name, kvp.Value.Dye, kvp.Key, kvp.Value.Gloss)))
            .GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public int Count
        => Data.Count;

    public bool ContainsKey(StainId key)
        => Data.ContainsKey(key.Value);

    public bool TryGetValue(StainId key, out Stain value)
    {
        if (!Data.TryGetValue(key.Value, out var data))
        {
            value = default;
            return false;
        }

        value = new Stain(data.Name, data.Dye, key.Value, data.Gloss);
        return true;
    }

    public Stain this[StainId key]
        => TryGetValue(key, out var data) ? data : throw new ArgumentOutOfRangeException(nameof(key));

    public IEnumerable<StainId> Keys
        => Data.Keys.Select(k => new StainId(k));

    public IEnumerable<Stain> Values
        => Data.Select(kvp => new Stain(kvp.Value.Name, kvp.Value.Dye, kvp.Key, kvp.Value.Gloss));
}
