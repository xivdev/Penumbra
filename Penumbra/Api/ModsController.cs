using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using System.Linq;

namespace Penumbra.Api;

public class ModsController : WebApiController
{
    private readonly Penumbra _penumbra;

    public ModsController( Penumbra penumbra )
        => _penumbra = penumbra;

    [Route( HttpVerbs.Get, "/mods" )]
    public object? GetMods()
    {
        return Penumbra.ModManager.Zip( Penumbra.CollectionManager.Current.ActualSettings ).Select( x => new
        {
            x.Second?.Enabled,
            x.Second?.Priority,
            FolderName = x.First.ModPath.Name,
            x.First.Name,
            BasePath = x.First.ModPath.FullName,
            Files    = x.First.AllFiles,
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
                o => o.Value.Path.FullName
            );
    }
}