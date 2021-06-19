using System.Collections.Generic;
using System.IO;
using System.Linq;
using Penumbra.Structs;
using Penumbra.Util;

namespace Penumbra.Mod
{
    public static class ModFunctions
    {
        public static bool CleanUpCollection( Dictionary< string, ModSettings > settings, IEnumerable< DirectoryInfo > modPaths )
        {
            var hashes      = modPaths.Select( p => p.Name ).ToHashSet();
            var missingMods = settings.Keys.Where( k => !hashes.Contains( k ) ).ToArray();
            var anyChanges  = false;
            foreach( var toRemove in missingMods )
            {
                anyChanges |= settings.Remove( toRemove );
            }

            return anyChanges;
        }

        public static HashSet< GamePath > GetFilesForConfig( RelPath relPath, ModSettings settings, ModMeta meta )
        {
            var doNotAdd = false;
            var files    = new HashSet< GamePath >();
            foreach( var group in meta.Groups.Values.Where( g => g.Options.Count > 0 ) )
            {
                doNotAdd |= group.ApplyGroupFiles( relPath, settings.Settings[ group.GroupName ], files );
            }

            if( !doNotAdd )
            {
                files.Add( new GamePath( relPath ) );
            }

            return files;
        }

        public static ModSettings ConvertNamedSettings( NamedModSettings namedSettings, ModMeta meta )
        {
            ModSettings ret = new()
            {
                Priority = namedSettings.Priority,
                Settings = namedSettings.Settings.Keys.ToDictionary( k => k, _ => 0 ),
            };

            foreach( var kvp in namedSettings.Settings )
            {
                if( !meta.Groups.TryGetValue( kvp.Key, out var info ) )
                {
                    continue;
                }

                if( info.SelectionType == SelectType.Single )
                {
                    if( namedSettings.Settings[ kvp.Key ].Count == 0 )
                    {
                        ret.Settings[ kvp.Key ] = 0;
                    }
                    else
                    {
                        var idx = info.Options.FindIndex( o => o.OptionName == namedSettings.Settings[ kvp.Key ].Last() );
                        ret.Settings[ kvp.Key ] = idx < 0 ? 0 : idx;
                    }
                }
                else
                {
                    foreach( var idx in namedSettings.Settings[ kvp.Key ]
                       .Select( option => info.Options.FindIndex( o => o.OptionName == option ) )
                       .Where( idx => idx >= 0 ) )
                    {
                        ret.Settings[ kvp.Key ] |= 1 << idx;
                    }
                }
            }

            return ret;
        }
    }
}