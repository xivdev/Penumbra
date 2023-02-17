using EmbedIO.Routing;
using EmbedIO;
using EmbedIO.WebApi;
using Penumbra.Api.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Penumbra.Api {
    public class ReloadController : WebApiController {
        private readonly Penumbra _penumbra;
        public ReloadController( Penumbra penumbra )
       => _penumbra = penumbra;

        [Route( HttpVerbs.Post, "/reload" )]
        public async Task Reload() {
            var data = await HttpContext.GetRequestDataAsync<ReloadData>();
            if( !string.IsNullOrEmpty( data.ModPath ) && !string.IsNullOrEmpty( data.ModName ) ) {
                if(Directory.Exists( data.ModPath ) ) {
                    _penumbra.Api.AddMod( data.ModPath );
                }
                _penumbra.Api.ReloadMod( data.ModPath, data.ModName );
            }
        }

        public class ReloadData {
            public string ModPath { get; set; } = string.Empty;
            public string ModName { get; set; } = string.Empty;
        }
    }
}
