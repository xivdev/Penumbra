using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using Penumbra.GameData.Enums;

namespace Penumbra.Api
{
    public class RedrawController : WebApiController
    {
        private readonly Penumbra _penumbra;

        public RedrawController( Penumbra penumbra )
            => _penumbra = penumbra;

        [Route( HttpVerbs.Post, "/redraw" )]
        public async Task Redraw()
        {
            RedrawData data = await HttpContext.GetRequestDataAsync<RedrawData>();
            _penumbra.Api.RedrawObject( data.Name, data.Type );
        }

        [Route( HttpVerbs.Post, "/redrawAll" )]
        public void RedrawAll()
        {
            _penumbra.Api.RedrawAll(RedrawType.WithoutSettings);
        }

        public class RedrawData
        {
            public string Name { get; set; } = string.Empty;
            public RedrawType Type { get; set; } = RedrawType.WithSettings;
        }
    }
}