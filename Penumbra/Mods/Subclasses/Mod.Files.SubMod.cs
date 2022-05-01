using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.ByteString;
using Penumbra.Import;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Mods;

public partial class Mod
{
    internal string DefaultFile
        => Path.Combine( BasePath.FullName, "default_mod.json" );

    // The default mod contains setting-independent sets of file replacements, file swaps and meta changes.
    // Every mod has an default mod, though it may be empty.
    private void SaveDefaultMod()
    {
        var defaultFile = DefaultFile;

        using var stream = File.Exists( defaultFile )
            ? File.Open( defaultFile, FileMode.Truncate )
            : File.Open( defaultFile, FileMode.CreateNew );

        using var w = new StreamWriter( stream );
        using var j = new JsonTextWriter( w );
        j.Formatting = Formatting.Indented;
        var serializer = new JsonSerializer
        {
            Formatting = Formatting.Indented,
        };
        ISubMod.WriteSubMod( j, serializer, _default, BasePath, 0 );
    }

    private void LoadDefaultOption()
    {
        var defaultFile = DefaultFile;
        try
        {
            if( !File.Exists( defaultFile ) )
            {
                _default.Load( BasePath, new JObject(), out _ );
            }
            else
            {
                _default.Load( BasePath, JObject.Parse( File.ReadAllText( defaultFile ) ), out _ );
            }
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not parse default file for {Name}:\n{e}" );
        }
    }


    // A sub mod is a collection of
    //   - file replacements
    //   - file swaps
    //   - meta manipulations
    // that can be used either as an option or as the default data for a mod.
    // It can be loaded and reloaded from Json.
    // Nothing is checked for existence or validity when loading.
    // Objects are also not checked for uniqueness, the first appearance of a game path or meta path decides.
    private sealed class SubMod : ISubMod
    {
        public string Name { get; set; } = "Default";

        public Dictionary< Utf8GamePath, FullPath > FileData         = new();
        public Dictionary< Utf8GamePath, FullPath > FileSwapData     = new();
        public HashSet< MetaManipulation >          ManipulationData = new();

        public IReadOnlyDictionary< Utf8GamePath, FullPath > Files
            => FileData;

        public IReadOnlyDictionary< Utf8GamePath, FullPath > FileSwaps
            => FileSwapData;

        public IReadOnlySet< MetaManipulation > Manipulations
            => ManipulationData;

        // Insert all changes from the other submod.
        // Overwrites already existing changes in this mod.
        public void MergeIn( ISubMod other )
        {
            foreach( var (key, value) in other.Files )
            {
                FileData[ key ] = value;
            }

            foreach( var (key, value) in other.FileSwaps )
            {
                FileSwapData[ key ] = value;
            }

            foreach( var manip in other.Manipulations )
            {
                ManipulationData.Remove( manip );
                ManipulationData.Add( manip );
            }
        }

        public void Load( DirectoryInfo basePath, JToken json, out int priority )
        {
            FileData.Clear();
            FileSwapData.Clear();
            ManipulationData.Clear();

            // Every option has a name, but priorities are only relevant for multi group options.
            Name     = json[ nameof( ISubMod.Name ) ]?.ToObject< string >()    ?? string.Empty;
            priority = json[ nameof( IModGroup.Priority ) ]?.ToObject< int >() ?? 0;

            var files = ( JObject? )json[ nameof( Files ) ];
            if( files != null )
            {
                foreach( var property in files.Properties() )
                {
                    if( Utf8GamePath.FromString( property.Name, out var p, true ) )
                    {
                        FileData.TryAdd( p, new FullPath( basePath, property.Value.ToObject< Utf8RelPath >() ) );
                    }
                }
            }

            var swaps = ( JObject? )json[ nameof( FileSwaps ) ];
            if( swaps != null )
            {
                foreach( var property in swaps.Properties() )
                {
                    if( Utf8GamePath.FromString( property.Name, out var p, true ) )
                    {
                        FileSwapData.TryAdd( p, new FullPath( property.Value.ToObject< string >()! ) );
                    }
                }
            }

            var manips = json[ nameof( Manipulations ) ];
            if( manips != null )
            {
                foreach( var s in manips.Children().Select( c => c.ToObject< MetaManipulation >() ) )
                {
                    ManipulationData.Add( s );
                }
            }
        }

        // If .meta or .rgsp files are encountered, parse them and incorporate their meta changes into the mod.
        // If delete is true, the files are deleted afterwards.
        public void IncorporateMetaChanges( DirectoryInfo basePath, bool delete )
        {
            foreach( var (key, file) in Files.ToList() )
            {
                try
                {
                    switch( file.Extension )
                    {
                        case ".meta":
                            FileData.Remove( key );
                            if( !file.Exists )
                            {
                                continue;
                            }

                            var meta = new TexToolsMeta( File.ReadAllBytes( file.FullName ) );
                            if( delete )
                            {
                                File.Delete( file.FullName );
                            }

                            ManipulationData.UnionWith( meta.MetaManipulations );

                            break;
                        case ".rgsp":
                            FileData.Remove( key );
                            if( !file.Exists )
                            {
                                continue;
                            }

                            var rgsp = TexToolsMeta.FromRgspFile( file.FullName, File.ReadAllBytes( file.FullName ) );
                            if( delete )
                            {
                                File.Delete( file.FullName );
                            }

                            ManipulationData.UnionWith( rgsp.MetaManipulations );

                            break;
                        default: continue;
                    }
                }
                catch( Exception e )
                {
                    PluginLog.Error( $"Could not incorporate meta changes in mod {basePath} from file {file.FullName}:\n{e}" );
                    continue;
                }
            }
        }
    }
}