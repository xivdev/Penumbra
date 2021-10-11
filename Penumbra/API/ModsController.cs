using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Logging;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.Utilities;
using EmbedIO.WebApi;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.Api
{
    public class ModsController : WebApiController
    {
        private readonly Penumbra _penumbra;

        public ModsController( Penumbra penumbra )
            => _penumbra = penumbra;

        [Route( HttpVerbs.Get, "/mods" )]
        public object? GetMods()
        {
            // Obtain Query Data
            var activeOnly          = Convert.ToBoolean( Request.QueryString[ "activeOnly" ] );
            var requestedCollection = Request.QueryString[ "collection" ];

            var modManager = Service< ModManager >.Get();
            var collection = modManager.Collections.CurrentCollection;

            if( !string.IsNullOrWhiteSpace(requestedCollection) )
            {
                collection = modManager.Collections.Collections[ requestedCollection ];
                if( collection.Cache is null )
                {
                    PluginLog.Log($"Collection {requestedCollection} has been requested but cash is null. Generating cache now....");
                    modManager.Collections.AddCache(collection);
                }
            }

            var mods = collection.Cache?.AvailableMods.Values.Select( x => new
            {
                x.Settings.Enabled,
                x.Settings.Priority,
                x.Data.BasePath.Name,
                x.Data.Meta,
                BasePath = x.Data.BasePath.FullName,
                Files    = x.Data.Resources.ModFiles.Select( fi => fi.FullName ),
            } );

            if( Convert.ToBoolean( Request.QueryString[ "activeOnly" ] ) )
            {
                mods = mods?.Where( m => m.Enabled );
            }

            return mods;
        }

        [Route( HttpVerbs.Post, "/mods" )]
        public object CreateMod()
            => new { };

        [Route(HttpVerbs.Post, "/mods/delete")]
        public async Task< bool > DeleteMod()
        {
            var requestData = await HttpContext.GetRequestFormDataAsync();
            var modName     = Request.QueryString[ "name" ];

            if( string.IsNullOrWhiteSpace(modName) )
            {
                return false;
            }

            PluginLog.Log($"Attempting to delete mod: {modName}");
            var modManager = Service< ModManager >.Get();
            var mod        = modManager.Mods[ modName ];
            modManager.DeleteMod(mod.BasePath);
            return true;
        }

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