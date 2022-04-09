using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Penumbra.Mods;

public partial class Mod2
{
    private sealed class SingleModGroup : IModGroup
    {
        public SelectType Type
            => SelectType.Single;

        public string Name { get; set; } = "Option";
        public string Description { get; set; } = "A mutually exclusive group of settings.";
        public int Priority { get; set; }

        public readonly List< SubMod > OptionData = new();

        public int OptionPriority( Index _ )
            => Priority;

        public ISubMod this[ Index idx ]
            => OptionData[ idx ];

        [JsonIgnore]
        public int Count
            => OptionData.Count;

        public IEnumerator< ISubMod > GetEnumerator()
            => OptionData.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        public static SingleModGroup? Load( JObject json, DirectoryInfo basePath )
        {
            var options = json[ "Options" ];
            var ret = new SingleModGroup
            {
                Name        = json[ nameof( Name ) ]?.ToObject< string >()        ?? string.Empty,
                Description = json[ nameof( Description ) ]?.ToObject< string >() ?? string.Empty,
                Priority    = json[ nameof( Priority ) ]?.ToObject< int >()       ?? 0,
            };
            if( ret.Name.Length == 0 )
            {
                return null;
            }

            if( options != null )
            {
                foreach( var child in options.Children() )
                {
                    var subMod = new SubMod();
                    subMod.Load( basePath, child, out _ );
                    ret.OptionData.Add( subMod );
                }
            }

            return ret;
        }
    }
}