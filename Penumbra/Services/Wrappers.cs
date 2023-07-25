using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Gui;
using Dalamud.Plugin;
using Penumbra.GameData;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Data;
using Penumbra.Interop.PathResolving;
using Penumbra.Util;

namespace Penumbra.Services;

public sealed class IdentifierService : AsyncServiceWrapper<IObjectIdentifier>
{
    public IdentifierService(StartTracker tracker, DalamudPluginInterface pi, DataManager data, ItemService items)
        : base(nameof(IdentifierService), tracker, StartTimeType.Identifier, () => GameData.GameData.GetIdentifier(pi, data, items.AwaitedService))
    { }
}

public sealed class ItemService : AsyncServiceWrapper<ItemData>
{
    public ItemService(StartTracker tracker, DalamudPluginInterface pi, DataManager gameData)
        : base(nameof(ItemService), tracker, StartTimeType.Items, () => new ItemData(pi, gameData, gameData.Language))
    { }
}

public sealed class ActorService : AsyncServiceWrapper<ActorManager>
{
    public ActorService(StartTracker tracker, DalamudPluginInterface pi, ObjectTable objects, ClientState clientState,
        Framework framework, DataManager gameData, GameGui gui, CutsceneService cutscene)
        : base(nameof(ActorService), tracker, StartTimeType.Actors,
            () => new ActorManager(pi, objects, clientState, framework, gameData, gui, idx => (short)cutscene.GetParentIndex(idx)))
    { }
}