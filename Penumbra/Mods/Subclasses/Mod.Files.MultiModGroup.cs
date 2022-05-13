using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui.Filesystem;

namespace Penumbra.Mods;

public partial class Mod
{
    // Groups that allow all available options to be selected at once.
    private sealed class MultiModGroup : IModGroup
    {
        public SelectType Type
            => SelectType.Multi;

        public string Name { get; set; } = "Group";
        public string Description { get; set; } = "A non-exclusive group of settings.";
        public int Priority { get; set; }

        public int OptionPriority( Index idx )
            => PrioritizedOptions[ idx ].Priority;

        public ISubMod this[ Index idx ]
            => PrioritizedOptions[ idx ].Mod;

        [JsonIgnore]
        public int Count
            => PrioritizedOptions.Count;

        public readonly List< (SubMod Mod, int Priority) > PrioritizedOptions = new();

        public IEnumerator< ISubMod > GetEnumerator()
            => PrioritizedOptions.Select( o => o.Mod ).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        public static MultiModGroup? Load( JObject json, DirectoryInfo basePath )
        {
            var options = json[ "Options" ];
            var ret = new MultiModGroup()
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
                    if( ret.PrioritizedOptions.Count == IModGroup.MaxMultiOptions )
                    {
                        PluginLog.Warning($"Multi Group {ret.Name} has more than {IModGroup.MaxMultiOptions} options, ignoring excessive options."  );
                        break;
                    }
                    var subMod = new SubMod();
                    subMod.Load( basePath, child, out var priority );
                    ret.PrioritizedOptions.Add( ( subMod, priority ) );
                }
            }

            return ret;
        }

        public IModGroup Convert( SelectType type )
        {
            switch( type )
            {
                case SelectType.Multi: return this;
                case SelectType.Single:
                    var multi = new SingleModGroup()
                    {
                        Name        = Name,
                        Description = Description,
                        Priority    = Priority,
                    };
                    multi.OptionData.AddRange( PrioritizedOptions.Select( p => p.Mod ) );
                    return multi;
                default: throw new ArgumentOutOfRangeException( nameof( type ), type, null );
            }
        }

        public bool MoveOption( int optionIdxFrom, int optionIdxTo )
            => PrioritizedOptions.Move( optionIdxFrom, optionIdxTo );
    }
}