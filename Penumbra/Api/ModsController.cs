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
        return Penumbra.ModManager.Mods.Zip( Penumbra.CollectionManager.Current.ActualSettings ).Select( x => new
        {
            x.Second?.Enabled,
            x.Second?.Priority,
            x.First.BasePath.Name,
            x.First.Meta,
            BasePath = x.First.BasePath.FullName,
            Files    = x.First.Resources.ModFiles.Select( fi => fi.FullName ),
        } );
    }

    [Route( HttpVerbs.Post, "/mods" )]
    public object CreateMod()
        => new { };

    [Route( HttpVerbs.Get, "/files" )]
    public object GetFiles()
    {
        return Penumbra.CollectionManager.Current.ResolvedFiles.ToDictionary(
                o => o.Key.ToString(),
                o => o.Value.FullName
            )
         ?? new Dictionary< string, string >();
    }
}