using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dalamud;
using Dalamud.Data;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Utility;
using Lumina.Excel.GeneratedSheets;

namespace Penumbra.GameData.Actors;

public partial class ActorManager : IDisposable
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

    public ActorManager(DalamudPluginInterface pluginInterface, ObjectTable objects, ClientState state, DataManager gameData, Func<ushort, short> toParentIdx)
        : this(pluginInterface, objects, state, gameData, gameData.Language, toParentIdx)
    {}

    public ActorManager(DalamudPluginInterface pluginInterface, ObjectTable objects, ClientState state, DataManager gameData,
        ClientLanguage language, Func<ushort, short> toParentIdx)
    {
        _pluginInterface = pluginInterface;
        _objects         = objects;
        _clientState     = state;
        _gameData        = gameData;
        _language        = language;
        _toParentIdx     = toParentIdx;

        Worlds     = TryCatchData("Worlds",     CreateWorldData);
        Mounts     = TryCatchData("Mounts",     CreateMountData);
        Companions = TryCatchData("Companions", CreateCompanionData);
        BNpcs      = TryCatchData("BNpcs",      CreateBNpcData);
        ENpcs      = TryCatchData("ENpcs",      CreateENpcData);

        ActorIdentifier.Manager = this;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        GC.SuppressFinalize(this);
        _pluginInterface.RelinquishData(GetVersionedTag("Worlds"));
        _pluginInterface.RelinquishData(GetVersionedTag("Mounts"));
        _pluginInterface.RelinquishData(GetVersionedTag("Companions"));
        _pluginInterface.RelinquishData(GetVersionedTag("BNpcs"));
        _pluginInterface.RelinquishData(GetVersionedTag("ENpcs"));
        _disposed = true;
    }

    ~ActorManager()
        => Dispose();

    private const int Version = 1;

    private readonly DalamudPluginInterface _pluginInterface;
    private readonly ObjectTable            _objects;
    private readonly ClientState            _clientState;
    private readonly DataManager            _gameData;
    private readonly ClientLanguage         _language;
    private          bool                   _disposed;

    private readonly Func<ushort, short> _toParentIdx;

    private IReadOnlyDictionary<ushort, string> CreateWorldData()
        => _gameData.GetExcelSheet<World>(_language)!
            .Where(w => w.IsPublic && !w.Name.RawData.IsEmpty)
            .ToDictionary(w => (ushort)w.RowId, w => w.Name.ToString());

    private IReadOnlyDictionary<uint, string> CreateMountData()
        => _gameData.GetExcelSheet<Mount>(_language)!
            .Where(m => m.Singular.RawData.Length > 0 && m.Order >= 0)
            .ToDictionary(m => m.RowId, m => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(m.Singular.ToDalamudString().ToString()));

    private IReadOnlyDictionary<uint, string> CreateCompanionData()
        => _gameData.GetExcelSheet<Companion>(_language)!
            .Where(c => c.Singular.RawData.Length > 0 && c.Order < ushort.MaxValue)
            .ToDictionary(c => c.RowId, c => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(c.Singular.ToDalamudString().ToString()));

    private IReadOnlyDictionary<uint, string> CreateBNpcData()
        => _gameData.GetExcelSheet<BNpcName>(_language)!
            .Where(n => n.Singular.RawData.Length > 0)
            .ToDictionary(n => n.RowId, n => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(n.Singular.ToDalamudString().ToString()));

    private IReadOnlyDictionary<uint, string> CreateENpcData()
        => _gameData.GetExcelSheet<ENpcResident>(_language)!
            .Where(e => e.Singular.RawData.Length > 0)
            .ToDictionary(e => e.RowId, e => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(e.Singular.ToDalamudString().ToString()));

    private string GetVersionedTag(string tag)
        => $"Penumbra.Actors.{tag}.{_language}.V{Version}";

    private T TryCatchData<T>(string tag, Func<T> func) where T : class
    {
        try
        {
            return _pluginInterface.GetOrCreateData(GetVersionedTag(tag), func);
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Error creating shared actor data for {tag}:\n{ex}");
            return func();
        }
    }
}
