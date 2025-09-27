using Dalamud.Plugin.Services;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using Penumbra.Api.Api;
using Penumbra.Api.Enums;
using Penumbra.Mods.Settings;

namespace Penumbra.Api;

public class HttpApi : IDisposable, Luna.IApiService
{
    private partial class Controller : WebApiController
    {
        // @formatter:off
        [Route( HttpVerbs.Get,  "/moddirectory"  )] public partial string GetModDirectory();
        [Route( HttpVerbs.Get,  "/mods"          )] public partial object? GetMods();
        [Route( HttpVerbs.Post, "/redraw"        )] public partial Task    Redraw();
        [Route( HttpVerbs.Post, "/redrawAll"     )] public partial Task    RedrawAll();
        [Route( HttpVerbs.Post, "/reloadmod"     )] public partial Task    ReloadMod();
        [Route( HttpVerbs.Post, "/installmod"    )] public partial Task    InstallMod();
        [Route( HttpVerbs.Post, "/openwindow"    )] public partial void    OpenWindow();
        [Route( HttpVerbs.Post, "/focusmod"      )] public partial Task    FocusMod();
        [Route( HttpVerbs.Post, "/setmodsettings")] public partial Task    SetModSettings();
        // @formatter:on
    }

    public const string Prefix = "http://localhost:42069/";

    private readonly IPenumbraApi _api;
    private readonly IFramework   _framework;
    private          WebServer?   _server;

    public HttpApi(Configuration config, IPenumbraApi api, IFramework framework)
    {
        _api       = api;
        _framework = framework;
        if (config.EnableHttpApi)
            CreateWebServer();
    }

    public bool Enabled
        => _server != null;

    public void CreateWebServer()
    {
        ShutdownWebServer();

        _server = new WebServer(o => o
                .WithUrlPrefix(Prefix)
                .WithMode(HttpListenerMode.EmbedIO))
            .WithCors(Prefix)
            .WithWebApi("/api", m => m.WithController(() => new Controller(_api, _framework)));

        _server.StateChanged += (_, e) => Penumbra.Log.Information($"WebServer New State - {e.NewState}");
        _server.RunAsync();
    }

    public void ShutdownWebServer()
    {
        _server?.Dispose();
        _server = null;
    }

    public void Dispose()
        => ShutdownWebServer();

    private partial class Controller(IPenumbraApi api, IFramework framework)
    {
        public partial string GetModDirectory()
        {
            Penumbra.Log.Debug($"[HTTP] {nameof(GetModDirectory)} triggered.");
            return api.PluginState.GetModDirectory();
        }

        public partial object? GetMods()
        {
            Penumbra.Log.Debug($"[HTTP] {nameof(GetMods)} triggered.");
            return api.Mods.GetModList();
        }

        public async partial Task Redraw()
        {
            var data = await HttpContext.GetRequestDataAsync<RedrawData>().ConfigureAwait(false);
            Penumbra.Log.Debug($"[HTTP] [{Environment.CurrentManagedThreadId}] {nameof(Redraw)} triggered with {data}.");
            await framework.RunOnFrameworkThread(() =>
            {
                if (data.ObjectTableIndex >= 0)
                    api.Redraw.RedrawObject(data.ObjectTableIndex, data.Type);
                else
                    api.Redraw.RedrawAll(data.Type);
            }).ConfigureAwait(false);
        }

        public async partial Task RedrawAll()
        {
            Penumbra.Log.Debug($"[HTTP] {nameof(RedrawAll)} triggered.");
            await framework.RunOnFrameworkThread(() => { api.Redraw.RedrawAll(RedrawType.Redraw); }).ConfigureAwait(false);
        }

        public async partial Task ReloadMod()
        {
            var data = await HttpContext.GetRequestDataAsync<ModReloadData>().ConfigureAwait(false);
            Penumbra.Log.Debug($"[HTTP] {nameof(ReloadMod)} triggered with {data}.");
            // Add the mod if it is not already loaded and if the directory name is given.
            // AddMod returns Success if the mod is already loaded.
            if (data.Path.Length != 0)
                api.Mods.AddMod(data.Path);

            // Reload the mod by path or name, which will also remove no-longer existing mods.
            api.Mods.ReloadMod(data.Path, data.Name);
        }

        public async partial Task InstallMod()
        {
            var data = await HttpContext.GetRequestDataAsync<ModInstallData>().ConfigureAwait(false);
            Penumbra.Log.Debug($"[HTTP] {nameof(InstallMod)} triggered with {data}.");
            if (data.Path.Length != 0)
                api.Mods.InstallMod(data.Path);
        }

        public partial void OpenWindow()
        {
            Penumbra.Log.Debug($"[HTTP] {nameof(OpenWindow)} triggered.");
            api.Ui.OpenMainWindow(TabType.Mods, string.Empty, string.Empty);
        }

        public async partial Task FocusMod()
        {
            var data = await HttpContext.GetRequestDataAsync<ModFocusData>().ConfigureAwait(false);
            Penumbra.Log.Debug($"[HTTP] {nameof(FocusMod)} triggered.");
            if (data.Path.Length != 0)
                api.Ui.OpenMainWindow(TabType.Mods, data.Path, data.Name);
        }

        public async partial Task SetModSettings()
        {
            var data = await HttpContext.GetRequestDataAsync<SetModSettingsData>().ConfigureAwait(false);
            Penumbra.Log.Debug($"[HTTP] {nameof(SetModSettings)} triggered.");
            await framework.RunOnFrameworkThread(() =>
                {
                    var collection = data.CollectionId ?? api.Collection.GetCollection(ApiCollectionType.Current)!.Value.Id;
                    if (data.Inherit.HasValue)
                    {
                        api.ModSettings.TryInheritMod(collection, data.ModPath, data.ModName, data.Inherit.Value);
                        if (data.Inherit.Value)
                            return;
                    }

                    if (data.State.HasValue)
                        api.ModSettings.TrySetMod(collection, data.ModPath, data.ModName, data.State.Value);
                    if (data.Priority.HasValue)
                        api.ModSettings.TrySetModPriority(collection, data.ModPath, data.ModName, data.Priority.Value.Value);
                    foreach (var (group, settings) in data.Settings ?? [])
                        api.ModSettings.TrySetModSettings(collection, data.ModPath, data.ModName, group, settings);
                }
            ).ConfigureAwait(false);
        }

        private record ModReloadData(string Path, string Name)
        {
            public ModReloadData()
                : this(string.Empty, string.Empty)
            { }
        }

        private record ModFocusData(string Path, string Name)
        {
            public ModFocusData()
                : this(string.Empty, string.Empty)
            { }
        }

        private record ModInstallData(string Path)
        {
            public ModInstallData()
                : this(string.Empty)
            { }
        }

        private record RedrawData(string Name, RedrawType Type, int ObjectTableIndex)
        {
            public RedrawData()
                : this(string.Empty, RedrawType.Redraw, -1)
            { }
        }

        private record SetModSettingsData(
            Guid? CollectionId,
            string ModPath,
            string ModName,
            bool? Inherit,
            bool? State,
            ModPriority? Priority,
            Dictionary<string, List<string>>? Settings)
        { }
    }
}
