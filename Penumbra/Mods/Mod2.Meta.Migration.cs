using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.ByteString;
using Penumbra.Util;

namespace Penumbra.Mods;

public sealed partial class Mod2
{
    private static class Migration
    {
        public static void Migrate( Mod2 mod, string text )
        {
            MigrateV0ToV1( mod, text );
        }

        private static void MigrateV0ToV1( Mod2 mod, string text )
        {
            if( mod.FileVersion > 0 )
            {
                return;
            }

            var data = JObject.Parse( text );
            var swaps = data[ "FileSwaps" ]?.ToObject< Dictionary< Utf8GamePath, FullPath > >()
             ?? new Dictionary< Utf8GamePath, FullPath >();
            var groups = data[ "Groups" ]?.ToObject< Dictionary< string, OptionGroupV0 > >() ?? new Dictionary< string, OptionGroupV0 >();
            foreach( var group in groups.Values )
            { }

            foreach( var swap in swaps )
            { }
        }


        private struct OptionV0
        {
            public string OptionName = string.Empty;
            public string OptionDesc = string.Empty;

            [JsonProperty( ItemConverterType = typeof( SingleOrArrayConverter< Utf8GamePath > ) )]
            public Dictionary< Utf8RelPath, HashSet< Utf8GamePath > > OptionFiles = new();

            public OptionV0()
            { }
        }

        private struct OptionGroupV0
        {
            public string GroupName = string.Empty;

            [JsonConverter( typeof( Newtonsoft.Json.Converters.StringEnumConverter ) )]
            public SelectType SelectionType = SelectType.Single;

            public List< OptionV0 > Options = new();

            public OptionGroupV0()
            { }
        }
    }
}