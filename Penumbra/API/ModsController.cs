using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Logging;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using Lumina.Data.Parsing;
using Lumina.Excel.GeneratedSheets;
using Penumbra.Api.Models;
using Penumbra.Mods;

namespace Penumbra.Api
{
    public class ModsController : WebApiController
    {
        private readonly Penumbra    _penumbra;
        private readonly PenumbraApi _api;
        private readonly ModManager  _modManager;

        public ModsController( Penumbra penumbra, ModManager modManager, PenumbraApi api )
        {
            _penumbra   = penumbra;
            _modManager = modManager;
            _api   = api;
        }

        /// <summary>
        /// Returns a list of all mods. Query params can be supplied to refine this list
        /// </summary>
        /// <param name="activeOnly">Returns only enabled mods if true</param>
        /// <param name="collection">Restricts the results to the specified collection if not null</param>
        [Route( HttpVerbs.Get, "/mods" )]
        public object? GetMods([QueryField] string collection, [QueryField] bool activeOnly)
        {
            var requestedCollection = string.IsNullOrWhiteSpace(collection)
                ? _modManager.Collections.CurrentCollection
                : _modManager.GetModCollection(collection);
            if( requestedCollection is null )
            {
                PluginLog.LogError("Unable to find any collections. Please ensure penumbra has at least one collection.");
                return false;
            }

            var mods = requestedCollection.Cache?.AvailableMods.Values.Select( x => new
            {
                x.Settings.Enabled,
                x.Settings.Priority,
                x.Data.BasePath.Name,
                x.Data.Meta,
                BasePath = x.Data.BasePath.FullName,
                Files    = x.Data.Resources.ModFiles.Select( fi => fi.FullName ),
            } );

            if( activeOnly )
            {
                mods = mods?.Where( m => m.Enabled );
            }

            return mods;
        }

        /// <summary>
        /// Creates an empty mod based on the form data posted to this endpoint
        /// </summary>
        /// <returns>Name of the created mod</returns>
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

        /// <summary>
        /// Deletes a mod from the user's mod folder
        /// </summary>
        /// <param name="name">Name of the mod to be deleted</param>
        /// <returns>Boolean reflecting if the mod was deleted successfully</returns>
        [Route(HttpVerbs.Post, "/mods/delete")]
        public bool DeleteMod([QueryField] string name)
        {
            if( string.IsNullOrWhiteSpace(name) )
            {
                return false;
            }

            PluginLog.Log($"Attempting to delete mod: {name}");
            try
            {
                var mod = _modManager.Mods[ name ];
                _modManager.DeleteMod( mod.BasePath );
                ModFileSystem.InvokeChange();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Obtains a list of items that have been modded. By default this will search the active collections only.
        /// </summary>
        /// <param name="collections">
        /// Name of the collection(s) you would like to limit the search to. Search multiple collections by adding commas
        /// </param>
        [Route( HttpVerbs.Get, "/mods/items" )]
        public object? GetChangedItems([QueryField] string collections)
        {
            Dictionary< string, ushort > changedItems         = new ();
            List< string >               requestedCollections = new ();

            if( string.IsNullOrWhiteSpace( collections ) )
            {
                requestedCollections.Add(_modManager.Collections.CurrentCollection.Name);
            }
            else if( collections.Contains( "," ) )
            {
                requestedCollections = collections.Split( ',' ).ToList();
            }
            else if( collections == "all" )
            {
                foreach( var collection in _modManager.Collections.Collections )
                {
                    requestedCollections.Add(collection.Key);
                }
            }
            else
            {
                requestedCollections.Add(collections);
            }

            foreach( var collection in requestedCollections )
            {
                ModCollection? requestedCollection = _modManager.GetModCollection( collection.Trim() );
                if( requestedCollection is null )
                {
                    continue;
                }

                var items = _api.GetChangedItemsForCollection( requestedCollection );
                foreach( var item in items )
                {
                    if( !changedItems.ContainsKey( item.Key ) )
                    {
                        changedItems.Add( item.Key, item.Value );
                    }
                }
            }
            return changedItems;
        }

        /// <summary>
        /// Get a list of files that have been modified by Penumbra
        /// </summary>
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