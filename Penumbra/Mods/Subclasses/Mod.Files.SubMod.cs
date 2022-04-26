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
    private string DefaultFile
        => Path.Combine( BasePath.FullName, "default_mod.json" );

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


    private sealed class SubMod : ISubMod
    {
        public string Name { get; set; } = "Default";

        public readonly Dictionary< Utf8GamePath, FullPath > FileData         = new();
        public readonly Dictionary< Utf8GamePath, FullPath > FileSwapData     = new();
        public readonly HashSet< MetaManipulation >          ManipulationData = new();

        public IReadOnlyDictionary< Utf8GamePath, FullPath > Files
            => FileData;

        public IReadOnlyDictionary< Utf8GamePath, FullPath > FileSwaps
            => FileSwapData;

        public IReadOnlySet< MetaManipulation > Manipulations
            => ManipulationData;

        public void Load( DirectoryInfo basePath, JToken json, out int priority )
        {
            FileData.Clear();
            FileSwapData.Clear();
            ManipulationData.Clear();

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