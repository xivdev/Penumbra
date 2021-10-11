using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Logging;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using Penumbra.Api.Models;
using Penumbra.Mods;

namespace Penumbra.Api
{
    public class ModsController : WebApiController
    {
        private readonly Penumbra   _penumbra;
        private readonly ModManager _modManager;

        public ModsController( Penumbra penumbra, ModManager modManager )
        {
            _penumbra   = penumbra;
            _modManager = modManager;
        }

        [Route( HttpVerbs.Get, "/mods" )]
        public object? GetMods()
        {
            // Obtain Query Data
            var activeOnly          = Convert.ToBoolean( Request.QueryString[ "activeOnly" ] );
            var requestedCollection = Request.QueryString[ "collection" ];

            var collection = _modManager.Collections.CurrentCollection;

            if( !string.IsNullOrWhiteSpace(requestedCollection) )
            {
                collection = _modManager.Collections.Collections[ requestedCollection ];
                if( collection.Cache is null )
                {
                    PluginLog.Log($"Collection {requestedCollection} has been requested but cash is null. Generating cache now....");
                    _modManager.Collections.AddCache(collection);
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
        public async Task< string > CreateMod()
        {
            var requestData = await HttpContext.GetRequestDataAsync<NewMod>();
            PluginLog.Log($"Attempting to create mod: {requestData.name}");
            var newModDir = _modManager.GenerateEmptyMod( requestData.name );
            return _modManager.Mods
                .Where( m => m.Value.BasePath.Name == newModDir.Name )
                .FirstOrDefault()
                .Value.BasePath.Name;
        }

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
            try
            {
                var mod = _modManager.Mods[ modName ];
                _modManager.DeleteMod( mod.BasePath );
                ModFileSystem.InvokeChange();

                return true;
            }
            catch
            {
                return false;
            }
        }

        [Route( HttpVerbs.Get, "/files" )]
        public object GetFiles()
        {
            return _modManager.Collections.CurrentCollection.Cache?.ResolvedFiles.ToDictionary(
                    o => ( string )o.Key,
                    o => o.Value.FullName
                )
             ?? new Dictionary< string, string >();
        }
    }
}