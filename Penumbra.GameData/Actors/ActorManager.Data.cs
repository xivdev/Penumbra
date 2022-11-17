using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dalamud;
using Dalamud.Data;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Gui;
using Dalamud.Plugin;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.GeneratedSheets;
using Lumina.Text;
using Penumbra.GameData.Data;
using Penumbra.String;
using Character = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

namespace Penumbra.GameData.Actors;

public sealed partial class ActorManager : DataSharer
{
    /// <summary> Worlds available for players. </summary>
    public IReadOnlyDictionary<ushort, string> Worlds { get; }

    /// <summary> Valid Mount names in title case by mount id. </summary>
    public IReadOnlyDictionary<uint, string> Mounts { get; }

    /// <summary> Valid Companion names in title case by companion id. </summary>
    public IReadOnlyDictionary<uint, string> Companions { get; }

    /// <summary> Valid ornament names by id. </summary>
    public IReadOnlyDictionary<uint, string> Ornaments { get; }

    /// <summary> Valid BNPC names in title case by BNPC Name id. </summary>
    public IReadOnlyDictionary<uint, string> BNpcs { get; }

    /// <summary> Valid ENPC names in title case by ENPC id. </summary>
    public IReadOnlyDictionary<uint, string> ENpcs { get; }

    public ActorManager(DalamudPluginInterface pluginInterface, ObjectTable objects, ClientState state, DataManager gameData, GameGui gameGui,
        Func<ushort, short> toParentIdx)
        : this(pluginInterface, objects, state, gameData, gameGui, gameData.Language, toParentIdx)
    { }

    public ActorManager(DalamudPluginInterface pluginInterface, ObjectTable objects, ClientState state, DataManager gameData, GameGui gameGui,
        ClientLanguage language, Func<ushort, short> toParentIdx)
        : base(pluginInterface, language, 1)
    {
        _objects     = objects;
        _gameGui     = gameGui;
        _clientState = state;
        _toParentIdx = toParentIdx;

        Worlds     = TryCatchData("Worlds",     () => CreateWorldData(gameData));
        Mounts     = TryCatchData("Mounts",     () => CreateMountData(gameData));
        Companions = TryCatchData("Companions", () => CreateCompanionData(gameData));
        Ornaments  = TryCatchData("Ornaments",  () => CreateOrnamentData(gameData));
        BNpcs      = TryCatchData("BNpcs",      () => CreateBNpcData(gameData));
        ENpcs      = TryCatchData("ENpcs",      () => CreateENpcData(gameData));

        ActorIdentifier.Manager = this;

        SignatureHelper.Initialise(this);
    }

    public unsafe ActorIdentifier GetCurrentPlayer()
    {
        var address = (Character*)(_objects[0]?.Address ?? IntPtr.Zero);
        return address == null
            ? ActorIdentifier.Invalid
            : CreateIndividualUnchecked(IdentifierType.Player, new ByteString(address->GameObject.Name), address->HomeWorld,
                ObjectKind.None,                               uint.MaxValue);
    }

    public ActorIdentifier GetInspectPlayer()
    {
        var addon = _gameGui.GetAddonByName("CharacterInspect", 1);
        if (addon == IntPtr.Zero)
            return ActorIdentifier.Invalid;

        return CreatePlayer(InspectName, InspectWorldId);
    }

    public unsafe ActorIdentifier GetCardPlayer()
    {
        var agent = AgentCharaCard.Instance();
        if (agent == null || agent->Data == null)
            return ActorIdentifier.Invalid;

        var worldId = *(ushort*)((byte*)agent->Data + 0xC0);
        return CreatePlayer(new ByteString(agent->Data->Name.StringPtr), worldId);
    }

    public ActorIdentifier GetGlamourPlayer()
    {
        var addon = _gameGui.GetAddonByName("MiragePrismMiragePlate", 1);
        return addon == IntPtr.Zero ? ActorIdentifier.Invalid : GetCurrentPlayer();
    }

    protected override void DisposeInternal()
    {
        DisposeTag("Worlds");
        DisposeTag("Mounts");
        DisposeTag("Companions");
        DisposeTag("Ornaments");
        DisposeTag("BNpcs");
        DisposeTag("ENpcs");
    }

    ~ActorManager()
        => Dispose();

    private readonly ObjectTable _objects;
    private readonly ClientState _clientState;
    private readonly GameGui     _gameGui;

    private readonly Func<ushort, short> _toParentIdx;

    private IReadOnlyDictionary<ushort, string> CreateWorldData(DataManager gameData)
        => gameData.GetExcelSheet<World>(Language)!
            .Where(w => w.IsPublic && !w.Name.RawData.IsEmpty)
            .ToDictionary(w => (ushort)w.RowId, w => w.Name.ToString());

    private IReadOnlyDictionary<uint, string> CreateMountData(DataManager gameData)
        => gameData.GetExcelSheet<Mount>(Language)!
            .Where(m => m.Singular.RawData.Length > 0 && m.Order >= 0)
            .ToDictionary(m => m.RowId, m => ToTitleCaseExtended(m.Singular, m.Article));

    private IReadOnlyDictionary<uint, string> CreateCompanionData(DataManager gameData)
        => gameData.GetExcelSheet<Companion>(Language)!
            .Where(c => c.Singular.RawData.Length > 0 && c.Order < ushort.MaxValue)
            .ToDictionary(c => c.RowId, c => ToTitleCaseExtended(c.Singular, c.Article));

    private IReadOnlyDictionary<uint, string> CreateOrnamentData(DataManager gameData)
        => gameData.GetExcelSheet<Ornament>(Language)!
            .Where(o => o.Singular.RawData.Length > 0)
            .ToDictionary(o => o.RowId, o => ToTitleCaseExtended(o.Singular, o.Article));

    private IReadOnlyDictionary<uint, string> CreateBNpcData(DataManager gameData)
        => gameData.GetExcelSheet<BNpcName>(Language)!
            .Where(n => n.Singular.RawData.Length > 0)
            .ToDictionary(n => n.RowId, n => ToTitleCaseExtended(n.Singular, n.Article));

    private IReadOnlyDictionary<uint, string> CreateENpcData(DataManager gameData)
        => gameData.GetExcelSheet<ENpcResident>(Language)!
            .Where(e => e.Singular.RawData.Length > 0)
            .ToDictionary(e => e.RowId, e => ToTitleCaseExtended(e.Singular, e.Article));

    private static string ToTitleCaseExtended(SeString s, sbyte article)
    {
        if (article == 1)
            return s.ToDalamudString().ToString();

        var sb        = new StringBuilder(s.ToDalamudString().ToString());
        var lastSpace = true;
        for (var i = 0; i < sb.Length; ++i)
        {
            if (sb[i] == ' ')
            {
                lastSpace = true;
            }
            else if (lastSpace)
            {
                lastSpace = false;
                sb[i]     = char.ToUpperInvariant(sb[i]);
            }
        }

        return sb.ToString();
    }


    [Signature("0F B7 0D ?? ?? ?? ?? C7 85", ScanType = ScanType.StaticAddress)]
    private static unsafe ushort* _inspectTitleId = null!;

    [Signature("0F B7 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B D0", ScanType = ScanType.StaticAddress)]
    private static unsafe ushort* _inspectWorldId = null!;

    private static unsafe ushort InspectTitleId
        => *_inspectTitleId;

    private static unsafe ByteString InspectName
        => new((byte*)(_inspectWorldId + 1));

    private static unsafe ushort InspectWorldId
        => *_inspectWorldId;
}
