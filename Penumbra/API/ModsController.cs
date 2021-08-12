using System.Collections.Generic;
using System.Linq;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.Api
{
    public class ModsController : WebApiController
    {
        private readonly Plugin _plugin;

        public ModsController( Plugin plugin )
            => _plugin = plugin;

        [Route( HttpVerbs.Get, "/mods" )]
        public object? GetMods()
        {
            var modManager = Service< ModManager >.Get();
            return modManager.Collections.CurrentCollection.Cache?.AvailableMods.Values.Select( x => new
                {
                    x.Settings.Enabled,
                    x.Settings.Priority,
                    x.Data.BasePath.Name,
                    x.Data.Meta,
                    BasePath = x.Data.BasePath.FullName,
                    Files    = x.Data.Resources.ModFiles.Select( fi => fi.FullName ),
                } )
             ?? null;
        }

        [Route( HttpVerbs.Post, "/mods" )]
        public object CreateMod()
            => new { };

        [Route( HttpVerbs.Get, "/files" )]
        public object GetFiles()
        {
            var modManager = Service< ModManager >.Get();
            return modManager.Collections.CurrentCollection.Cache?.ResolvedFiles.ToDictionary(
                    o => ( string )o.Key,
                    o => o.Value.FullName
                )
             ?? new Dictionary< string, string >();
        }
    }
}