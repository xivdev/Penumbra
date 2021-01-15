using System.Linq;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;

namespace Penumbra.API
{
    public class ModsController : WebApiController
    {
        private readonly Plugin _plugin;

        public ModsController( Plugin plugin )
        {
            _plugin = plugin;
        }

        [Route( HttpVerbs.Get, "/mods" )]
        public object GetMods()
        {
            return _plugin.ModManager.Mods.ModSettings.Select( x => new
            {
                x.Enabled,
                x.Priority,
                x.FolderName,
                x.Mod.Meta,
                BasePath = x.Mod.ModBasePath.FullName,
                Files = x.Mod.ModFiles.Select( fi => fi.FullName )
            } );
        }

        [Route( HttpVerbs.Get, "/files" )]
        public object CreateMod()
        {
            return _plugin.ModManager.ResolvedFiles.ToDictionary(
                o => o.Key,
                o => o.Value.FullName
            );
        }
    }
}