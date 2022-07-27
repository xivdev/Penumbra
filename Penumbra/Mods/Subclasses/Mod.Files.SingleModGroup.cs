using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui;
using OtterGui.Filesystem;

namespace Penumbra.Mods;

public partial class Mod
{
    // Groups that allow only one of their available options to be selected.
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

        public static SingleModGroup? Load( Mod mod, JObject json, int groupIdx )
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
                    var subMod = new SubMod( mod );
                    subMod.SetPosition( groupIdx, ret.OptionData.Count );
                    subMod.Load( mod.ModPath, child, out _ );
                    ret.OptionData.Add( subMod );
                }
            }

            return ret;
        }

        public IModGroup Convert( SelectType type )
        {
            switch( type )
            {
                case SelectType.Single: return this;
                case SelectType.Multi:
                    var multi = new MultiModGroup()
                    {
                        Name        = Name,
                        Description = Description,
                        Priority    = Priority,
                    };
                    multi.PrioritizedOptions.AddRange( OptionData.Select( ( o, i ) => ( o, i ) ) );
                    return multi;
                default: throw new ArgumentOutOfRangeException( nameof( type ), type, null );
            }
        }

        public bool MoveOption( int optionIdxFrom, int optionIdxTo )
        {
            if( !OptionData.Move( optionIdxFrom, optionIdxTo ) )
            {
                return false;
            }

            UpdatePositions( Math.Min( optionIdxFrom, optionIdxTo ) );
            return true;
        }

        public void UpdatePositions( int from = 0 )
        {
            foreach( var (o, i) in OptionData.WithIndex().Skip( from ) )
            {
                o.SetPosition( o.GroupIdx, i );
            }
        }
    }
}