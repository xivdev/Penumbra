using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Dalamud.Logging;
using Newtonsoft.Json;

namespace Penumbra.Mods;

public partial class Mod2
{
    private sealed class SingleModGroup : IModGroup
    {
        public SelectType Type
            => SelectType.Single;

        public string Name { get; set; } = "Option";
        public string Description { get; set; } = "A mutually exclusive group of settings.";
        public int Priority { get; set; } = 0;

        public readonly List< SubMod > OptionData = new();

        public int OptionPriority( Index _ )
            => Priority;

        public ISubMod this[ Index idx ]
            => OptionData[ idx ];

        public int Count
            => OptionData.Count;

        public IEnumerator< ISubMod > GetEnumerator()
            => OptionData.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        public void Save( DirectoryInfo basePath )
        {
            var path = ( ( IModGroup )this ).FileName( basePath );
            try
            {
                var text = JsonConvert.SerializeObject( this, Formatting.Indented );
                File.WriteAllText( path, text );
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Could not save option group {Name} to {path}:\n{e}" );
            }
        }
    }
}