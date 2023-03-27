using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Penumbra.Import;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;

namespace Penumbra.Mods;

/// <summary>
/// A sub mod is a collection of
/// - file replacements
///   - file swaps
///   - meta manipulations
/// that can be used either as an option or as the default data for a mod.
/// It can be loaded and reloaded from Json.
/// Nothing is checked for existence or validity when loading.
/// Objects are also not checked for uniqueness, the first appearance of a game path or meta path decides.
/// </summary>
  public sealed class SubMod : ISubMod
{
    public string Name { get; set; } = "Default";

    public string FullName
        => GroupIdx < 0 ? "Default Option" : $"{ParentMod.Groups[ GroupIdx ].Name}: {Name}";

    public string Description { get; set; } = string.Empty;

    internal IMod ParentMod { get; private init; }
    internal int  GroupIdx  { get; private set; }
    internal int  OptionIdx { get; private set; }

    public bool IsDefault
        => GroupIdx < 0;

    public Dictionary< Utf8GamePath, FullPath > FileData         = new();
    public Dictionary< Utf8GamePath, FullPath > FileSwapData     = new();
    public HashSet< MetaManipulation >          ManipulationData = new();

    public SubMod( IMod parentMod )
        => ParentMod = parentMod;

    public IReadOnlyDictionary< Utf8GamePath, FullPath > Files
        => FileData;

    public IReadOnlyDictionary< Utf8GamePath, FullPath > FileSwaps
        => FileSwapData;

    public IReadOnlySet< MetaManipulation > Manipulations
        => ManipulationData;

    public void SetPosition( int groupIdx, int optionIdx )
    {
        GroupIdx  = groupIdx;
        OptionIdx = optionIdx;
    }

    public void Load( DirectoryInfo basePath, JToken json, out int priority )
    {
        FileData.Clear();
        FileSwapData.Clear();
        ManipulationData.Clear();

        // Every option has a name, but priorities are only relevant for multi group options.
        Name        = json[ nameof( ISubMod.Name ) ]?.ToObject< string >()        ?? string.Empty;
        Description = json[ nameof( ISubMod.Description ) ]?.ToObject< string >() ?? string.Empty;
        priority    = json[ nameof( IModGroup.Priority ) ]?.ToObject< int >()     ?? 0;

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
            foreach( var s in manips.Children().Select( c => c.ToObject< MetaManipulation >() ).Where( m => m.ManipulationType != MetaManipulation.Type.Unknown ) )
            {
                ManipulationData.Add( s );
            }
        }
    }

    // If .meta or .rgsp files are encountered, parse them and incorporate their meta changes into the mod.
    // If delete is true, the files are deleted afterwards.
    public (bool Changes, List< string > DeleteList) IncorporateMetaChanges( DirectoryInfo basePath, bool delete )
    {
        var deleteList   = new List< string >();
        var oldSize      = ManipulationData.Count;
        var deleteString = delete ? "with deletion." : "without deletion.";
        foreach( var (key, file) in Files.ToList() )
        {
            var ext1 = key.Extension().AsciiToLower().ToString();
            var ext2 = file.Extension.ToLowerInvariant();
            try
            {
                if( ext1 == ".meta" || ext2 == ".meta" )
                {
                    FileData.Remove( key );
                    if( !file.Exists )
                    {
                        continue;
                    }

                    var meta = new TexToolsMeta( Penumbra.GamePathParser, File.ReadAllBytes( file.FullName ), Penumbra.Config.KeepDefaultMetaChanges );
                    Penumbra.Log.Verbose( $"Incorporating {file} as Metadata file of {meta.MetaManipulations.Count} manipulations {deleteString}" );
                    deleteList.Add( file.FullName );
                    ManipulationData.UnionWith( meta.MetaManipulations );
                }
                else if( ext1 == ".rgsp" || ext2 == ".rgsp" )
                {
                    FileData.Remove( key );
                    if( !file.Exists )
                    {
                        continue;
                    }

                    var rgsp = TexToolsMeta.FromRgspFile( file.FullName, File.ReadAllBytes( file.FullName ), Penumbra.Config.KeepDefaultMetaChanges );
                    Penumbra.Log.Verbose( $"Incorporating {file} as racial scaling file of {rgsp.MetaManipulations.Count} manipulations {deleteString}" );
                    deleteList.Add( file.FullName );

                    ManipulationData.UnionWith( rgsp.MetaManipulations );
                }
            }
            catch( Exception e )
            {
                Penumbra.Log.Error( $"Could not incorporate meta changes in mod {basePath} from file {file.FullName}:\n{e}" );
            }
        }

        DeleteDeleteList( deleteList, delete );
        return ( oldSize < ManipulationData.Count, deleteList );
    }

    internal static void DeleteDeleteList( IEnumerable< string > deleteList, bool delete )
    {
        if( !delete )
        {
            return;
        }

        foreach( var file in deleteList )
        {
            try
            {
                File.Delete( file );
            }
            catch( Exception e )
            {
                Penumbra.Log.Error( $"Could not delete incorporated meta file {file}:\n{e}" );
            }
        }
    }

    public void WriteTexToolsMeta( DirectoryInfo basePath, bool test = false )
    {
        var files = TexToolsMeta.ConvertToTexTools( Manipulations );

        foreach( var (file, data) in files )
        {
            var path = Path.Combine( basePath.FullName, file );
            try
            {
                Directory.CreateDirectory( Path.GetDirectoryName( path )! );
                File.WriteAllBytes( path, data );
            }
            catch( Exception e )
            {
                Penumbra.Log.Error( $"Could not write meta file {path}:\n{e}" );
            }
        }

        if( test )
        {
            TestMetaWriting( files );
        }
    }

    [Conditional("DEBUG" )]
    private void TestMetaWriting( Dictionary< string, byte[] > files )
    {
        var meta = new HashSet< MetaManipulation >( Manipulations.Count );
        foreach( var (file, data) in files )
        {
            try
            {
                var x = file.EndsWith( "rgsp" )
                    ? TexToolsMeta.FromRgspFile( file, data, Penumbra.Config.KeepDefaultMetaChanges )
                    : new TexToolsMeta( Penumbra.GamePathParser, data, Penumbra.Config.KeepDefaultMetaChanges );
                meta.UnionWith( x.MetaManipulations );
            }
            catch
            {
                // ignored
            }
        }

        if( !Manipulations.SetEquals( meta ) )
        {
            Penumbra.Log.Information( "Meta Sets do not equal." );
            foreach( var (m1, m2) in Manipulations.Zip( meta ) )
            {
                Penumbra.Log.Information( $"{m1} {m1.EntryToString()} | {m2} {m2.EntryToString()}" );
            }

            foreach( var m in Manipulations.Skip( meta.Count ) )
            {
                Penumbra.Log.Information( $"{m} {m.EntryToString()} " );
            }

            foreach( var m in meta.Skip( Manipulations.Count ) )
            {
                Penumbra.Log.Information( $"{m} {m.EntryToString()} " );
            }
        }
        else
        {
            Penumbra.Log.Information( "Meta Sets are equal." );
        }
    }
}