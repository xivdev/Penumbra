using System.Collections.Generic;
using System.Linq;
using System;

namespace Penumbra.Models
{
    public class ModSettings
    {
        public int Priority { get; set; }
        public Dictionary< string, int > Settings { get; set; } = new();

        // For backwards compatibility
        private Dictionary< string, int > Conf
        {
            set => Settings = value;
        }

        public static ModSettings CreateFrom( NamedModSettings n, ModMeta meta )
        {
            ModSettings ret = new()
            {
                Priority = n.Priority,
                Settings = n.Settings.Keys.ToDictionary( K => K, K => 0 )
            };

            foreach( var kvp in n.Settings )
            {
                if( !meta.Groups.TryGetValue( kvp.Key, out var info ) )
                {
                    continue;
                }

                if( info.SelectionType == SelectType.Single )
                {
                    if( n.Settings[ kvp.Key ].Count == 0 )
                    {
                        ret.Settings[ kvp.Key ] = 0;
                    }
                    else
                    {
                        var idx = info.Options.FindIndex( O => O.OptionName == n.Settings[ kvp.Key ].Last() );
                        ret.Settings[ kvp.Key ] = idx < 0 ? 0 : idx;
                    }
                }
                else
                {
                    foreach( var idx in n.Settings[ kvp.Key ]
                        .Select( option => info.Options.FindIndex( O => O.OptionName == option ) )
                        .Where( idx => idx >= 0 ) )
                    {
                        ret.Settings[ kvp.Key ] |= 1 << idx;
                    }
                }
            }

            return ret;
        }

        public bool FixSpecificSetting( ModMeta meta, string name )
        {
            if( !meta.Groups.TryGetValue( name, out var group ) )
            {
                return Settings.Remove( name );
            }

            if( Settings.TryGetValue( name, out var oldSetting ) )
            {
                Settings[ name ] = group.SelectionType switch
                {
                    SelectType.Single => Math.Min( Math.Max( oldSetting, 0 ), group.Options.Count - 1 ),
                    SelectType.Multi  => Math.Min( Math.Max( oldSetting, 0 ), ( 1 << group.Options.Count ) - 1 ),
                    _                 => Settings[ group.GroupName ]
                };
                return oldSetting != Settings[ group.GroupName ];
            }

            Settings[ name ] = 0;
            return true;
        }

        public bool FixInvalidSettings( ModMeta meta )
        {
            if( meta.Groups.Count == 0 )
            {
                return false;
            }

            return Settings.Keys.ToArray().Union( meta.Groups.Keys )
                .Aggregate( false, ( current, name ) => current | FixSpecificSetting( meta, name ) );
        }
    }
}