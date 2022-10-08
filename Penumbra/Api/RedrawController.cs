using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using System.Threading.Tasks;
using Penumbra.Api.Enums;

namespace Penumbra.Api;

public class RedrawController : WebApiController
{
    private readonly Penumbra _penumbra;

    public RedrawController( Penumbra penumbra )
        => _penumbra = penumbra;

    [Route( HttpVerbs.Post, "/redraw" )]
    public async Task Redraw()
    {
        var data = await HttpContext.GetRequestDataAsync< RedrawData >();
        if( data.ObjectTableIndex >= 0 )
        {
            _penumbra.Api.RedrawObject( data.ObjectTableIndex, data.Type );
        }
        else if( data.Name.Length > 0 )
        {
            _penumbra.Api.RedrawObject( data.Name, data.Type );
        }
        else
        {
            _penumbra.Api.RedrawAll( data.Type );
        }
    }

    [Route( HttpVerbs.Post, "/redrawAll" )]
    public void RedrawAll()
    {
        _penumbra.Api.RedrawAll( RedrawType.Redraw );
    }

    public class RedrawData
    {
        public string Name { get; set; } = string.Empty;
        public RedrawType Type { get; set; } = RedrawType.Redraw;
        public int ObjectTableIndex { get; set; } = -1;
    }
}