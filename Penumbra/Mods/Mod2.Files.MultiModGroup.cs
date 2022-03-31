using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Newtonsoft.Json;

namespace Penumbra.Mods;

public partial class Mod2
{
    private sealed class MultiModGroup : IModGroup
    {
        public SelectType Type
            => SelectType.Multi;

        public string Name { get; set; } = "Group";
        public string Description { get; set; } = "A non-exclusive group of settings.";
        public int Priority { get; set; } = 0;

        public int OptionPriority( Index idx )
            => PrioritizedOptions[ idx ].Priority;

        public ISubMod this[ Index idx ]
            => PrioritizedOptions[ idx ].Mod;

        public int Count
            => PrioritizedOptions.Count;

        public readonly List< (SubMod Mod, int Priority) > PrioritizedOptions = new();

        public IEnumerator< ISubMod > GetEnumerator()
            => PrioritizedOptions.Select( o => o.Mod ).GetEnumerator();

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