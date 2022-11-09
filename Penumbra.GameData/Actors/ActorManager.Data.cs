using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dalamud;
using Dalamud.Data;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin;
using Dalamud.Utility;
using Lumina.Excel.GeneratedSheets;
using Penumbra.GameData.Data;

namespace Penumbra.GameData.Actors;

public sealed partial class ActorManager : DataSharer
{
    /// <summary> Worlds available for players. </summary>
    public IReadOnlyDictionary<ushort, string> Worlds { get; }

    /// <summary> Valid Mount names in title case by mount id. </summary>
    public IReadOnlyDictionary<uint, string> Mounts { get; }

    /// <summary> Valid Companion names in title case by companion id. </summary>
    public IReadOnlyDictionary<uint, string> Companions { get; }

    /// <summary> Valid BNPC names in title case by BNPC Name id. </summary>
    public IReadOnlyDictionary<uint, string> BNpcs { get; }

    /// <summary> Valid ENPC names in title case by ENPC id. </summary>
    public IReadOnlyDictionary<uint, string> ENpcs { get; }

    public ActorManager(DalamudPluginInterface pluginInterface, ObjectTable objects, ClientState state, DataManager gameData,
        Func<ushort, short> toParentIdx)
        : this(pluginInterface, objects, state, gameData, gameData.Language, toParentIdx)
    { }

    public ActorManager(DalamudPluginInterface pluginInterface, ObjectTable objects, ClientState state, DataManager gameData,
        ClientLanguage language, Func<ushort, short> toParentIdx)
        : base(pluginInterface, language, 1)
    {
        _objects     = objects;
        _clientState = state;
        _toParentIdx = toParentIdx;

        Worlds     = TryCatchData("Worlds",     () => CreateWorldData(gameData));
        Mounts     = TryCatchData("Mounts",     () => CreateMountData(gameData));
        Companions = TryCatchData("Companions", () => CreateCompanionData(gameData));
        BNpcs      = TryCatchData("BNpcs",      () => CreateBNpcData(gameData));
        ENpcs      = TryCatchData("ENpcs",      () => CreateENpcData(gameData));

        ActorIdentifier.Manager = this;
    }

    protected override void DisposeInternal()
    {
        DisposeTag("Worlds");
        DisposeTag("Mounts");
        DisposeTag("Companions");
        DisposeTag("BNpcs");
        DisposeTag("ENpcs");
    }

    ~ActorManager()
        => Dispose();

    private readonly ObjectTable _objects;
    private readonly ClientState _clientState;

    private readonly Func<ushort, short> _toParentIdx;

    private IReadOnlyDictionary<ushort, string> CreateWorldData(DataManager gameData)
        => gameData.GetExcelSheet<World>(Language)!
            .Where(w => w.IsPublic && !w.Name.RawData.IsEmpty)
            .ToDictionary(w => (ushort)w.RowId, w => w.Name.ToString());

    private IReadOnlyDictionary<uint, string> CreateMountData(DataManager gameData)
        => gameData.GetExcelSheet<Mount>(Language)!
            .Where(m => m.Singular.RawData.Length > 0 && m.Order >= 0)
            .ToDictionary(m => m.RowId, m => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(m.Singular.ToDalamudString().ToString()));

    private IReadOnlyDictionary<uint, string> CreateCompanionData(DataManager gameData)
        => gameData.GetExcelSheet<Companion>(Language)!
            .Where(c => c.Singular.RawData.Length > 0 && c.Order < ushort.MaxValue)
            .ToDictionary(c => c.RowId, c => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(c.Singular.ToDalamudString().ToString()));

    private IReadOnlyDictionary<uint, string> CreateBNpcData(DataManager gameData)
        => gameData.GetExcelSheet<BNpcName>(Language)!
            .Where(n => n.Singular.RawData.Length > 0)
            .ToDictionary(n => n.RowId, n => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(n.Singular.ToDalamudString().ToString()));

    private IReadOnlyDictionary<uint, string> CreateENpcData(DataManager gameData)
        => gameData.GetExcelSheet<ENpcResident>(Language)!
            .Where(e => e.Singular.RawData.Length > 0)
            .ToDictionary(e => e.RowId, e => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(e.Singular.ToDalamudString().ToString()));
}
