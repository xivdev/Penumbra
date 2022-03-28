using System.Collections.Generic;
using System.IO;
using System.Linq;
using Penumbra.GameData.ByteString;

namespace Penumbra.Mods;

// Functions that do not really depend on only one component of a mod.
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

    public static HashSet< Utf8GamePath > GetFilesForConfig( Utf8RelPath relPath, ModSettings settings, ModMeta meta )
    {
        var doNotAdd = false;
        var files    = new HashSet< Utf8GamePath >();
        foreach( var group in meta.Groups.Values.Where( g => g.Options.Count > 0 ) )
        {
            doNotAdd |= group.ApplyGroupFiles( relPath, settings.Settings[ group.GroupName ], files );
        }

        if( !doNotAdd )
        {
            files.Add( relPath.ToGamePath() );
        }

        return files;
    }

    public static HashSet< Utf8GamePath > GetAllFiles( Utf8RelPath relPath, ModMeta meta )
    {
        var ret = new HashSet< Utf8GamePath >();
        foreach( var option in meta.Groups.Values.SelectMany( g => g.Options ) )
        {
            if( option.OptionFiles.TryGetValue( relPath, out var files ) )
            {
                ret.UnionWith( files );
            }
        }

        if( ret.Count == 0 )
        {
            ret.Add( relPath.ToGamePath() );
        }

        return ret;
    }

    public static ModSettings ConvertNamedSettings( NamedModSettings namedSettings, ModMeta meta )
    {
        ModSettings ret = new()
        {
            Priority = namedSettings.Priority,
            Settings = namedSettings.Settings.Keys.ToDictionary( k => k, _ => 0 ),
        };

        foreach( var setting in namedSettings.Settings.Keys )
        {
            if( !meta.Groups.TryGetValue( setting, out var info ) )
            {
                continue;
            }

            if( info.SelectionType == SelectType.Single )
            {
                if( namedSettings.Settings[ setting ].Count == 0 )
                {
                    ret.Settings[ setting ] = 0;
                }
                else
                {
                    var idx = info.Options.FindIndex( o => o.OptionName == namedSettings.Settings[ setting ].Last() );
                    ret.Settings[ setting ] = idx < 0 ? 0 : idx;
                }
            }
            else
            {
                foreach( var idx in namedSettings.Settings[ setting ]
                           .Select( option => info.Options.FindIndex( o => o.OptionName == option ) )
                           .Where( idx => idx >= 0 ) )
                {
                    ret.Settings[ setting ] |= 1 << idx;
                }
            }
        }

        return ret;
    }
}