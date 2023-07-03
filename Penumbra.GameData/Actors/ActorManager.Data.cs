using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Dalamud;
using Dalamud.Data;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Gui;
using Dalamud.Plugin;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using Lumina.Text;
using Penumbra.GameData.Data;
using Penumbra.GameData.Structs;
using Penumbra.String;
using Character = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace Penumbra.GameData.Actors;

public sealed partial class ActorManager : IDisposable
{
    public sealed class ActorManagerData : DataSharer
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

        public ActorManagerData(DalamudPluginInterface pluginInterface, DataManager gameData, ClientLanguage language)
            : base(pluginInterface, language, 1)
        {
            var worldTask      = TryCatchDataAsync("Worlds",     CreateWorldData(gameData));
            var mountsTask     = TryCatchDataAsync("Mounts",     CreateMountData(gameData));
            var companionsTask = TryCatchDataAsync("Companions", CreateCompanionData(gameData));
            var ornamentsTask  = TryCatchDataAsync("Ornaments",  CreateOrnamentData(gameData));
            var bNpcsTask      = TryCatchDataAsync("BNpcs",      CreateBNpcData(gameData));
            var eNpcsTask      = TryCatchDataAsync("ENpcs",      CreateENpcData(gameData));

            Worlds     = worldTask.Result;
            Mounts     = mountsTask.Result;
            Companions = companionsTask.Result;
            Ornaments  = ornamentsTask.Result;
            BNpcs      = bNpcsTask.Result;
            ENpcs      = eNpcsTask.Result;
        }

        /// <summary>
        /// Return the world name including the Any World option.
        /// </summary>
        public string ToWorldName(ushort worldId)
            => worldId == ushort.MaxValue ? "Any World" : Worlds.TryGetValue(worldId, out var name) ? name : "Invalid";

        /// <summary>
        /// Return the world id corresponding to the given name.
        /// </summary>
        /// <returns>ushort.MaxValue if the name is empty, 0 if it is not a valid world, or the worlds id.</returns>
        public ushort ToWorldId(string worldName)
            => worldName.Length != 0
                ? Worlds.FirstOrDefault(kvp => string.Equals(kvp.Value, worldName, StringComparison.OrdinalIgnoreCase), default).Key
                : ushort.MaxValue;

        /// <summary>
        /// Convert a given ID for a certain ObjectKind to a name.
        /// </summary>
        /// <returns>Invalid or a valid name.</returns>
        public string ToName(ObjectKind kind, uint dataId)
            => TryGetName(kind, dataId, out var ret) ? ret : "Invalid";


        /// <summary>
        /// Convert a given ID for a certain ObjectKind to a name.
        /// </summary>
        public bool TryGetName(ObjectKind kind, uint dataId, [NotNullWhen(true)] out string? name)
        {
            name = null;
            return kind switch
            {
                ObjectKind.MountType => Mounts.TryGetValue(dataId, out name),
                ObjectKind.Companion => Companions.TryGetValue(dataId, out name),
                ObjectKind.Ornament  => Ornaments.TryGetValue(dataId, out name),
                ObjectKind.BattleNpc => BNpcs.TryGetValue(dataId, out name),
                ObjectKind.EventNpc  => ENpcs.TryGetValue(dataId, out name),
                _                    => false,
            };
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

        private Action<Dictionary<ushort, string>> CreateWorldData(DataManager gameData)
            => d =>
            {
                foreach (var w in gameData.GetExcelSheet<World>(Language)!.Where(w => w.IsPublic && !w.Name.RawData.IsEmpty))
                    d.TryAdd((ushort)w.RowId, string.Intern(w.Name.ToDalamudString().TextValue));
            };

        private Action<Dictionary<uint, string>> CreateMountData(DataManager gameData)
            => d =>
            {
                foreach (var m in gameData.GetExcelSheet<Mount>(Language)!.Where(m => m.Singular.RawData.Length > 0 && m.Order >= 0))
                    d.TryAdd(m.RowId, ToTitleCaseExtended(m.Singular, m.Article));
            };

        private Action<Dictionary<uint, string>> CreateCompanionData(DataManager gameData)
            => d =>
            {
                foreach (var c in gameData.GetExcelSheet<Companion>(Language)!.Where(c
                             => c.Singular.RawData.Length > 0 && c.Order < ushort.MaxValue))
                    d.TryAdd(c.RowId, ToTitleCaseExtended(c.Singular, c.Article));
            };

        private Action<Dictionary<uint, string>> CreateOrnamentData(DataManager gameData)
            => d =>
            {
                foreach (var o in gameData.GetExcelSheet<Ornament>(Language)!.Where(o => o.Singular.RawData.Length > 0))
                    d.TryAdd(o.RowId, ToTitleCaseExtended(o.Singular, o.Article));
            };

        private Action<Dictionary<uint, string>> CreateBNpcData(DataManager gameData)
            => d =>
            {
                foreach (var n in gameData.GetExcelSheet<BNpcName>(Language)!.Where(n => n.Singular.RawData.Length > 0))
                    d.TryAdd(n.RowId, ToTitleCaseExtended(n.Singular, n.Article));
            };

        private Action<Dictionary<uint, string>> CreateENpcData(DataManager gameData)
            => d =>
            {
                foreach (var n in gameData.GetExcelSheet<ENpcResident>(Language)!.Where(e => e.Singular.RawData.Length > 0))
                    d.TryAdd(n.RowId, ToTitleCaseExtended(n.Singular, n.Article));
            };

        private static string ToTitleCaseExtended(SeString s, sbyte article)
        {
            if (article == 1)
                return string.Intern(s.ToDalamudString().ToString());

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

            return string.Intern(sb.ToString());
        }
    }

    public readonly ActorManagerData Data;

    public ActorManager(DalamudPluginInterface pluginInterface, ObjectTable objects, ClientState state, Dalamud.Game.Framework framework,
        DataManager gameData, GameGui gameGui,
        Func<ushort, short> toParentIdx)
        : this(pluginInterface, objects, state, framework, gameData, gameGui, gameData.Language, toParentIdx)
    { }

    public ActorManager(DalamudPluginInterface pluginInterface, ObjectTable objects, ClientState state, Dalamud.Game.Framework framework,
        DataManager gameData, GameGui gameGui,
        ClientLanguage language, Func<ushort, short> toParentIdx)
    {
        _framework   = framework;
        _objects     = objects;
        _gameGui     = gameGui;
        _clientState = state;
        _toParentIdx = toParentIdx;
        Data         = new ActorManagerData(pluginInterface, gameData, language);

        ActorIdentifier.Manager = this;

        SignatureHelper.Initialise(this);
    }

    public unsafe ActorIdentifier GetCurrentPlayer()
    {
        var address = (Character*)_objects.GetObjectAddress(0);
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

    public unsafe bool ResolvePartyBannerPlayer(ScreenActor type, out ActorIdentifier id)
    {
        id = ActorIdentifier.Invalid;
        var module = Framework.Instance()->GetUiModule()->GetAgentModule();
        if (module == null)
            return false;

        var agent = (AgentBannerInterface*)module->GetAgentByInternalId(AgentId.BannerParty);
        if (agent == null || !agent->AgentInterface.IsAgentActive())
            agent = (AgentBannerInterface*)module->GetAgentByInternalId(AgentId.BannerMIP);
        if (agent == null || !agent->AgentInterface.IsAgentActive())
            return false;

        var idx       = (ushort)type - (ushort)ScreenActor.CharacterScreen;
        var character = agent->Character(idx);
        if (character == null)
            return true;

        var name = new ByteString(character->Name1.StringPtr);
        id = CreatePlayer(name, (ushort)character->WorldId);
        return true;
    }

    private unsafe bool SearchPlayerCustomize(Character* character, int idx, out ActorIdentifier id)
    {
        var other = (Character*)_objects.GetObjectAddress(idx);
        if (other == null || !CustomizeData.ScreenActorEquals((CustomizeData*)character->DrawData.CustomizeData.Data, (CustomizeData*)other->DrawData.CustomizeData.Data))
        {
            id = ActorIdentifier.Invalid;
            return false;
        }

        id = FromObject(&other->GameObject, out _, false, true, false);
        return true;
    }

    private unsafe ActorIdentifier SearchPlayersCustomize(Character* gameObject, int idx1, int idx2, int idx3)
        => SearchPlayerCustomize(gameObject,  idx1, out var ret)
         || SearchPlayerCustomize(gameObject, idx2, out ret)
         || SearchPlayerCustomize(gameObject, idx3, out ret)
                ? ret
                : ActorIdentifier.Invalid;

    private unsafe ActorIdentifier SearchPlayersCustomize(Character* gameObject)
    {
        static bool Compare(Character* a, Character* b)
        {
            var data1  = (CustomizeData*)a->DrawData.CustomizeData.Data;
            var data2  = (CustomizeData*)b->DrawData.CustomizeData.Data;
            var equals = CustomizeData.ScreenActorEquals(data1, data2);
            return equals;
        }

        for (var i = 0; i < (int)ScreenActor.CutsceneStart; i += 2)
        {
            var obj = (GameObject*)_objects.GetObjectAddress(i);
            if (obj != null
             && obj->ObjectKind is (byte)ObjectKind.Player
             && Compare(gameObject, (Character*)obj))
                return FromObject(obj, out _, false, true, false);
        }

        return ActorIdentifier.Invalid;
    }

    public unsafe bool ResolveMahjongPlayer(ScreenActor type, out ActorIdentifier id)
    {
        id = ActorIdentifier.Invalid;
        if (_clientState.TerritoryType != 831 && _gameGui.GetAddonByName("EmjIntro") == IntPtr.Zero)
            return false;

        var obj = (Character*)_objects.GetObjectAddress((int)type);
        if (obj == null)
            return false;

        id = type switch
        {
            ScreenActor.CharacterScreen => GetCurrentPlayer(),
            ScreenActor.ExamineScreen   => SearchPlayersCustomize(obj, 2, 4, 6),
            ScreenActor.FittingRoom     => SearchPlayersCustomize(obj, 4, 2, 6),
            ScreenActor.DyePreview      => SearchPlayersCustomize(obj, 6, 2, 4),
            _                           => ActorIdentifier.Invalid,
        };
        return true;
    }

    public unsafe bool ResolvePvPBannerPlayer(ScreenActor type, out ActorIdentifier id)
    {
        id = ActorIdentifier.Invalid;
        if (!_clientState.IsPvPExcludingDen)
            return false;

        var addon = (AtkUnitBase*)_gameGui.GetAddonByName("PvPMap");
        if (addon == null || addon->IsVisible)
            return false;

        var obj = (Character*)_objects.GetObjectAddress((int)type);
        if (obj == null)
            return false;

        id = type switch
        {
            ScreenActor.CharacterScreen => SearchPlayersCustomize(obj),
            ScreenActor.ExamineScreen   => SearchPlayersCustomize(obj),
            ScreenActor.FittingRoom     => SearchPlayersCustomize(obj),
            ScreenActor.DyePreview      => SearchPlayersCustomize(obj),
            ScreenActor.Portrait        => SearchPlayersCustomize(obj),
            _                           => ActorIdentifier.Invalid,
        };
        return true;
    }

    public unsafe ActorIdentifier GetCardPlayer()
    {
        var agent = AgentCharaCard.Instance();
        if (agent == null || agent->Data == null)
            return ActorIdentifier.Invalid;

        var worldId = *(ushort*)((byte*)agent->Data + Offsets.AgentCharaCardDataWorldId);
        return CreatePlayer(new ByteString(agent->Data->Name.StringPtr), worldId);
    }

    public ActorIdentifier GetGlamourPlayer()
    {
        var addon = _gameGui.GetAddonByName("MiragePrismMiragePlate", 1);
        return addon == IntPtr.Zero ? ActorIdentifier.Invalid : GetCurrentPlayer();
    }

    public void Dispose()
    {
        Data.Dispose();
        if (ActorIdentifier.Manager == this)
            ActorIdentifier.Manager = null;
    }

    ~ActorManager()
        => Dispose();

    private readonly Dalamud.Game.Framework _framework;
    private readonly ObjectTable            _objects;
    private readonly ClientState            _clientState;
    private readonly GameGui                _gameGui;

    private readonly Func<ushort, short> _toParentIdx;

    [Signature(Sigs.InspectTitleId, ScanType = ScanType.StaticAddress)]
    private static unsafe ushort* _inspectTitleId = null!;

    [Signature(Sigs.InspectWorldId, ScanType = ScanType.StaticAddress)]
    private static unsafe ushort* _inspectWorldId = null!;

    private static unsafe ushort InspectTitleId
        => *_inspectTitleId;

    private static unsafe ByteString InspectName
        => new((byte*)(_inspectWorldId + 1));

    private static unsafe ushort InspectWorldId
        => *_inspectWorldId;

    public static readonly IReadOnlySet<uint> MannequinIds = new HashSet<uint>()
    {
        1026228u,
        1026229u,
        1026986u,
        1026987u,
        1026988u,
        1026989u,
        1032291u,
        1032292u,
        1032293u,
        1032294u,
        1033046u,
        1033047u,
        1033658u,
        1033659u,
        1007137u,
        // TODO: Female Hrothgar
    };
}
