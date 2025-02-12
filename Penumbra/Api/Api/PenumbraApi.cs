using OtterGui.Services;

namespace Penumbra.Api.Api;

public class PenumbraApi(
    CollectionApi collection,
    EditingApi editing,
    GameStateApi gameState,
    MetaApi meta,
    ModsApi mods,
    ModSettingsApi modSettings,
    PluginStateApi pluginState,
    RedrawApi redraw,
    ResolveApi resolve,
    ResourceTreeApi resourceTree,
    TemporaryApi temporary,
    UiApi ui) : IDisposable, IApiService, IPenumbraApi
{
    public void Dispose()
    {
        Valid = false;
    }

    public (int Breaking, int Feature) ApiVersion
        => (5, 7);

    public bool                     Valid        { get; private set; } = true;
    public IPenumbraApiCollection   Collection   { get; }              = collection;
    public IPenumbraApiEditing      Editing      { get; }              = editing;
    public IPenumbraApiGameState    GameState    { get; }              = gameState;
    public IPenumbraApiMeta         Meta         { get; }              = meta;
    public IPenumbraApiMods         Mods         { get; }              = mods;
    public IPenumbraApiModSettings  ModSettings  { get; }              = modSettings;
    public IPenumbraApiPluginState  PluginState  { get; }              = pluginState;
    public IPenumbraApiRedraw       Redraw       { get; }              = redraw;
    public IPenumbraApiResolve      Resolve      { get; }              = resolve;
    public IPenumbraApiResourceTree ResourceTree { get; }              = resourceTree;
    public IPenumbraApiTemporary    Temporary    { get; }              = temporary;
    public IPenumbraApiUi           Ui           { get; }              = ui;
}
