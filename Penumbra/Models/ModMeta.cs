using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Penumbra.Util;

namespace Penumbra.Models
{
    public class ModMeta
    {
        public uint FileVersion { get; set; }
        public string Name { get; set; } = "Mod";
        public string Author { get; set; } = "";
        public string Description { get; set; } = "";

        public string Version { get; set; } = "";

        public string Website { get; set; } = "";

        public List< string > ChangedItems { get; set; } = new();

        public Dictionary< GamePath, GamePath > FileSwaps { get; } = new();

        public Dictionary< string, OptionGroup > Groups { get; set; } = new();

        [JsonIgnore]
        public bool HasGroupWithConfig { get; set; } = false;

        public static ModMeta? LoadFromFile( string filePath )
        {
            try
            {
                var meta = JsonConvert.DeserializeObject< ModMeta >( File.ReadAllText( filePath ) );
                meta.HasGroupWithConfig = meta.Groups.Count > 0
                    && meta.Groups.Values.Any( G => G.SelectionType == SelectType.Multi || G.Options.Count > 1 );

                return meta;
            }
            catch( Exception )
            {
                return null;
                // todo: handle broken mods properly
            }
        }

        private static bool ApplySingleGroupFiles( OptionGroup group, RelPath relPath, int selection, HashSet< GamePath > paths )
        {
            if( group.Options[ selection ].OptionFiles.TryGetValue( relPath, out var groupPaths ) )
            {
                paths.UnionWith( groupPaths );
                return true;
            }

            for( var i = 0; i < group.Options.Count; ++i )
            {
                if( i == selection )
                {
                    continue;
                }

                if( group.Options[ i ].OptionFiles.ContainsKey( relPath ) )
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ApplyMultiGroupFiles( OptionGroup group, RelPath relPath, int selection, HashSet< GamePath > paths )
        {
            var doNotAdd = false;
            for( var i = 0; i < group.Options.Count; ++i )
            {
                if( ( selection & ( 1 << i ) ) != 0 )
                {
                    if( group.Options[ i ].OptionFiles.TryGetValue( relPath, out var groupPaths ) )
                    {
                        paths.UnionWith( groupPaths );
                    }
                }
                else if( group.Options[ i ].OptionFiles.ContainsKey( relPath ) )
                {
                    doNotAdd = true;
                }
            }

            return doNotAdd;
        }

        public (bool configChanged, HashSet< GamePath > paths) GetFilesForConfig( RelPath relPath, ModSettings settings )
        {
            var doNotAdd      = false;
            var configChanged = false;

            HashSet< GamePath > paths = new();
            foreach( var group in Groups.Values )
            {
                configChanged |= settings.FixSpecificSetting( this, group.GroupName );

                if( group.Options.Count == 0 )
                {
                    continue;
                }

                switch( group.SelectionType )
                {
                    case SelectType.Single:
                        doNotAdd |= ApplySingleGroupFiles( group, relPath, settings.Settings[ group.GroupName ], paths );
                        break;
                    case SelectType.Multi:
                        doNotAdd |= ApplyMultiGroupFiles( group, relPath, settings.Settings[ group.GroupName ], paths );
                        break;
                }
            }

            if( !doNotAdd )
            {
                paths.Add( new GamePath( relPath ) );
            }

            return ( configChanged, paths );
        }
    }
}