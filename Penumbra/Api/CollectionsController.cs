using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Logging;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using Penumbra.GameData.Enums;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.Api
{
    public class CollectionsController : WebApiController
    {
        [Route( HttpVerbs.Get, "/collections" )]
        public object? GetCollections()
        {
            var collectionsManager = Service<ModManager>.Get().Collections;

            List<string> collections = new List<string>();
            foreach( (string name, ModCollection collection) in collectionsManager.Collections )
            {
                collections.Add( name );
            }

            return collections;
        }

        [Route( HttpVerbs.Get, "/collections/{name}" )]
        public object? GetCollection(string name)
        {
            var collectionsManager = Service<ModManager>.Get().Collections;

            if( collectionsManager.Collections.TryGetValue( name, out var collection ) )
                return collection;

            return null;
        }
    }
}
