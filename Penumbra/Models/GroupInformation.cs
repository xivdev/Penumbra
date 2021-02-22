using System.Collections.Generic;
using Newtonsoft.Json;
using Penumbra.Util;

namespace Penumbra.Models
{
    public enum SelectType
    {
        Single,
        Multi
    }

    public struct Option
    {
        public string OptionName;
        public string OptionDesc;

        [JsonProperty( ItemConverterType = typeof( SingleOrArrayConverter< GamePath > ) )]
        public Dictionary< RelPath, HashSet< GamePath > > OptionFiles;

        public bool AddFile( RelPath filePath, GamePath gamePath )
        {
            if( OptionFiles.TryGetValue( filePath, out var set ) )
            {
                return set.Add( gamePath );
            }

            OptionFiles[ filePath ] = new HashSet< GamePath >() { gamePath };
            return true;
        }
    }

    public struct OptionGroup
    {
        public string GroupName;

        [JsonConverter( typeof( Newtonsoft.Json.Converters.StringEnumConverter ) )]
        public SelectType SelectionType;

        public List< Option > Options;
    }
}