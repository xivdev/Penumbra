using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using Penumbra.GameData.Util;
using Penumbra.Util;

namespace Penumbra.Structs
{
    public enum SelectType
    {
        Single,
        Multi,
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

        private bool ApplySingleGroupFiles( RelPath relPath, int selection, HashSet< GamePath > paths )
        {
            if( Options[ selection ].OptionFiles.TryGetValue( relPath, out var groupPaths ) )
            {
                paths.UnionWith( groupPaths );
                return true;
            }

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

        private bool ApplyMultiGroupFiles( RelPath relPath, int selection, HashSet< GamePath > paths )
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
        internal bool ApplyGroupFiles( RelPath relPath, int selection, HashSet< GamePath > paths )
        {
            return SelectionType switch
            {
                SelectType.Single => ApplySingleGroupFiles( relPath, selection, paths ),
                SelectType.Multi  => ApplyMultiGroupFiles( relPath, selection, paths ),
                _                 => throw new InvalidEnumArgumentException( "Invalid option group type." ),
            };
        }
    }
}