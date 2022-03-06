using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using Penumbra.GameData.ByteString;
using Penumbra.Util;

namespace Penumbra.Mod;

public enum SelectType
{
    Single,
    Multi,
}

public struct Option
{
    public string OptionName;
    public string OptionDesc;

    [JsonProperty( ItemConverterType = typeof( SingleOrArrayConverter< Utf8GamePath > ) )]
    public Dictionary< Utf8RelPath, HashSet< Utf8GamePath > > OptionFiles;

    public bool AddFile( Utf8RelPath filePath, Utf8GamePath gamePath )
    {
        if( OptionFiles.TryGetValue( filePath, out var set ) )
        {
            return set.Add( gamePath );
        }

        OptionFiles[ filePath ] = new HashSet< Utf8GamePath > { gamePath };
        return true;
    }
}

public struct OptionGroup
{
    public string GroupName;

    [JsonConverter( typeof( Newtonsoft.Json.Converters.StringEnumConverter ) )]
    public SelectType SelectionType;

    public List< Option > Options;

    private bool ApplySingleGroupFiles( Utf8RelPath relPath, int selection, HashSet< Utf8GamePath > paths )
    {
        // Selection contains the path, merge all GamePaths for this config.
        if( Options[ selection ].OptionFiles.TryGetValue( relPath, out var groupPaths ) )
        {
            paths.UnionWith( groupPaths );
            return true;
        }

        // If the group contains the file in another selection, return true to skip it for default files.
        for( var i = 0; i < Options.Count; ++i )
        {
            if( i == selection )
            {
                continue;
            }

            if( Options[ i ].OptionFiles.ContainsKey( relPath ) )
            {
                return true;
            }
        }

        return false;
    }

    private bool ApplyMultiGroupFiles( Utf8RelPath relPath, int selection, HashSet< Utf8GamePath > paths )
    {
        var doNotAdd = false;
        for( var i = 0; i < Options.Count; ++i )
        {
            if( ( selection & ( 1 << i ) ) != 0 )
            {
                if( Options[ i ].OptionFiles.TryGetValue( relPath, out var groupPaths ) )
                {
                    paths.UnionWith( groupPaths );
                    doNotAdd = true;
                }
            }
            else if( Options[ i ].OptionFiles.ContainsKey( relPath ) )
            {
                doNotAdd = true;
            }
        }

        return doNotAdd;
    }

    // Adds all game paths from the given option that correspond to the given RelPath to paths, if any exist.
    internal bool ApplyGroupFiles( Utf8RelPath relPath, int selection, HashSet< Utf8GamePath > paths )
    {
        return SelectionType switch
        {
            SelectType.Single => ApplySingleGroupFiles( relPath, selection, paths ),
            SelectType.Multi  => ApplyMultiGroupFiles( relPath, selection, paths ),
            _                 => throw new InvalidEnumArgumentException( "Invalid option group type." ),
        };
    }
}