using System.Collections.Generic;
using System.Linq;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;

namespace Penumbra.Api;

public class ModsController : WebApiController
{
    private readonly Penumbra _penumbra;

    public ModsController( Penumbra penumbra )
        => _penumbra = penumbra;

    [Route( HttpVerbs.Get, "/mods" )]
    public object? GetMods()
    {
        return Penumbra.ModManager.Collections.CurrentCollection.Cache?.AvailableMods.Values.Select( x => new
            {
                x.Settings.Enabled,
                x.Settings.Priority,
                x.Data.BasePath.Name,
                x.Data.Meta,
                BasePath = x.Data.BasePath.FullName,
                Files    = x.Data.Resources.ModFiles.Select( fi => fi.FullName ),
            } );
    }

    [Route( HttpVerbs.Post, "/mods" )]
    public object CreateMod()
        => new { };

    [Route( HttpVerbs.Get, "/files" )]
    public object GetFiles()
    {
        return Penumbra.ModManager.Collections.CurrentCollection.Cache?.ResolvedFiles.ToDictionary(
                o => o.Key.ToString(),
                o => o.Value.FullName
            )
         ?? new Dictionary< string, string >();
    }
}