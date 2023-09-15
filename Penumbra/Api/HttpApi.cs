using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using Penumbra.Api.Enums;

namespace Penumbra.Api;

public class HttpApi : IDisposable
{
    private partial class Controller : WebApiController
    {
        // @formatter:off
        [Route( HttpVerbs.Get,  "/mods"       )] public partial object? GetMods();
        [Route( HttpVerbs.Post, "/redraw"     )] public partial Task    Redraw();
        [Route( HttpVerbs.Post, "/redrawAll"  )] public partial void    RedrawAll();
        [Route( HttpVerbs.Post, "/reloadmod"  )] public partial Task    ReloadMod();
        [Route( HttpVerbs.Post, "/installmod" )] public partial Task    InstallMod();
        [Route( HttpVerbs.Post, "/openwindow" )] public partial void    OpenWindow();
        // @formatter:on
    }

    public const string Prefix = "http://localhost:42069/";

    private readonly IPenumbraApi _api;
    private          WebServer?   _server;

    public HttpApi(Configuration config, IPenumbraApi api)
    {
        _api = api;
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
            .WithWebApi("/api", m => m.WithController(() => new Controller(_api)));

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

    private partial class Controller
    {
        private readonly IPenumbraApi _api;

        public Controller(IPenumbraApi api)
            => _api = api;

        public partial object? GetMods()
        {
            Penumbra.Log.Debug($"[HTTP] {nameof(GetMods)} triggered.");
            return _api.GetModList();
        }

        public async partial Task Redraw()
        {
            var data = await HttpContext.GetRequestDataAsync<RedrawData>();
            Penumbra.Log.Debug($"[HTTP] {nameof(Redraw)} triggered with {data}.");
            if (data.ObjectTableIndex >= 0)
                _api.RedrawObject(data.ObjectTableIndex, data.Type);
            else if (data.Name.Length > 0)
                _api.RedrawObject(data.Name, data.Type);
            else
                _api.RedrawAll(data.Type);
        }

        public partial void RedrawAll()
        {
            Penumbra.Log.Debug($"[HTTP] {nameof(RedrawAll)} triggered.");
            _api.RedrawAll(RedrawType.Redraw);
        }

        public async partial Task ReloadMod()
        {
            var data = await HttpContext.GetRequestDataAsync<ModReloadData>();
            Penumbra.Log.Debug($"[HTTP] {nameof(ReloadMod)} triggered with {data}.");
            // Add the mod if it is not already loaded and if the directory name is given.
            // AddMod returns Success if the mod is already loaded.
            if (data.Path.Length != 0)
                _api.AddMod(data.Path);

            // Reload the mod by path or name, which will also remove no-longer existing mods.
            _api.ReloadMod(data.Path, data.Name);
        }

        public async partial Task InstallMod()
        {
            var data = await HttpContext.GetRequestDataAsync<ModInstallData>();
            Penumbra.Log.Debug($"[HTTP] {nameof(InstallMod)} triggered with {data}.");
            if (data.Path.Length != 0)
                _api.InstallMod(data.Path);
        }

        public partial void OpenWindow()
        {
            Penumbra.Log.Debug($"[HTTP] {nameof(OpenWindow)} triggered.");
            _api.OpenMainWindow(TabType.Mods, string.Empty, string.Empty);
        }

        private record ModReloadData(string Path, string Name)
        {
            public ModReloadData()
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
    }
}
