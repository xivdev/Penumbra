using Dalamud.Game;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Penumbra.GameData;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Data;
using Penumbra.Interop.PathResolving;
using Penumbra.Util;

namespace Penumbra.Services;

public sealed class IdentifierService : AsyncServiceWrapper<IObjectIdentifier>
{
    public IdentifierService(StartTracker tracker, DalamudPluginInterface pi, IDataManager data, ItemService items, IPluginLog log)
        : base(nameof(IdentifierService), tracker, StartTimeType.Identifier,
            () => GameData.GameData.GetIdentifier(pi, data, items.AwaitedService, log))
    { }
}

public sealed class ItemService : AsyncServiceWrapper<ItemData>
{
    public ItemService(StartTracker tracker, DalamudPluginInterface pi, IDataManager gameData, IPluginLog log)
        : base(nameof(ItemService), tracker, StartTimeType.Items, () => new ItemData(pi, gameData, gameData.Language, log))
    { }
}

public sealed class ActorService : AsyncServiceWrapper<ActorManager>
{
    public ActorService(StartTracker tracker, DalamudPluginInterface pi, IObjectTable objects, IClientState clientState,
        IFramework framework, IDataManager gameData, IGameGui gui, CutsceneService cutscene, IPluginLog log, IGameInteropProvider interop)
        : base(nameof(ActorService), tracker, StartTimeType.Actors,
            () => new ActorManager(pi, objects, clientState, framework, interop, gameData, gui, idx => (short)cutscene.GetParentIndex(idx), log))
    { }
}
