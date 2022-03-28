using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Newtonsoft.Json;
using Penumbra.GameData.ByteString;
using Penumbra.Importer;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;

namespace Penumbra.Meta;

// Corresponds meta manipulations of any kind with the settings for a mod.
// DefaultData contains all manipulations that are active regardless of option groups.
// GroupData contains a mapping of Group -> { Options -> {Manipulations} }.
public class MetaCollection
{
    public List< MetaManipulation >                                             DefaultData = new();
    public Dictionary< string, Dictionary< string, List< MetaManipulation > > > GroupData   = new();


    // Store total number of manipulations for some ease of access.
    [JsonIgnore]
    internal int Count;


    // Return an enumeration of all active meta manipulations for a given mod with given settings.
    public IEnumerable< MetaManipulation > GetManipulationsForConfig( ModSettings settings, ModMeta modMeta )
    {
        if( Count == DefaultData.Count )
        {
            return DefaultData;
        }

        IEnumerable< MetaManipulation > ret = DefaultData;

        foreach( var group in modMeta.Groups )
        {
            if( !GroupData.TryGetValue( group.Key, out var metas ) || !settings.Settings.TryGetValue( group.Key, out var setting ) )
            {
                continue;
            }

            if( group.Value.SelectionType == SelectType.Single )
            {
                var settingName = group.Value.Options[ setting ].OptionName;
                if( metas.TryGetValue( settingName, out var meta ) )
                {
                    ret = ret.Concat( meta );
                }
            }
            else
            {
                for( var i = 0; i < group.Value.Options.Count; ++i )
                {
                    var flag = 1 << i;
                    if( ( setting & flag ) == 0 )
                    {
                        continue;
                    }

                    var settingName = group.Value.Options[ i ].OptionName;
                    if( metas.TryGetValue( settingName, out var meta ) )
                    {
                        ret = ret.Concat( meta );
                    }
                }
            }
        }

        return ret;
    }

    // Check that the collection is still basically valid,
    // i.e. keep it sorted, and verify that the options stored by name are all still part of the mod,
    // and that the contained manipulations are still valid and non-default manipulations.
    public bool Validate( ModMeta modMeta )
    {
        SortLists();
        foreach( var group in GroupData )
        {
            if( !modMeta.Groups.TryGetValue( group.Key, out var options ) )
            {
                return false;
            }

            foreach( var option in group.Value )
            {
                if( options.Options.All( o => o.OptionName != option.Key ) )
                {
                    return false;
                }

                //if( option.Value.Any( manip => defaultFiles.CheckAgainstDefault( manip ) ) )
                //{
                //    return false;
                //}
            }
        } // TODO

        return true; //DefaultData.All( manip => !defaultFiles.CheckAgainstDefault( manip ) );
    }

    // Re-sort all manipulations.
    private void SortLists()
    {
        DefaultData.Sort();
        foreach( var list in GroupData.Values.SelectMany( g => g.Values ) )
        {
            list.Sort();
        }
    }

    // Add a parsed TexTools .meta file to a given option group and option. If group is the empty string, add it to default.
    // Creates the option group and the option if necessary.
    private void AddMeta( string group, string option, TexToolsMeta meta )
    {
        var manipulations = meta.EqpManipulations.Select( m => new MetaManipulation( m ) )
           .Concat( meta.EqdpManipulations.Select( m => new MetaManipulation( m ) ) )
           .Concat( meta.EstManipulations.Select( m => new MetaManipulation( m ) ) )
           .Concat( meta.GmpManipulations.Select( m => new MetaManipulation( m ) ) )
           .Concat( meta.RspManipulations.Select( m => new MetaManipulation( m ) ) )
           .Concat( meta.ImcManipulations.Select( m => new MetaManipulation( m ) ) ).ToList();

        if( group.Length == 0 )
        {
            DefaultData.AddRange( manipulations );
        }
        else if( option.Length == 0 )
        { }
        else if( !GroupData.TryGetValue( group, out var options ) )
        {
            GroupData.Add( group, new Dictionary< string, List< MetaManipulation > >() { { option, manipulations } } );
        }
        else if( !options.TryGetValue( option, out var list ) )
        {
            options.Add( option, manipulations );
        }
        else
        {
            list.AddRange( manipulations );
        }

        Count += manipulations.Count;
    }

    // Update the whole meta collection by reading all TexTools .meta files in a mod directory anew,
    // combining them with the given ModMeta.
    public void Update( IEnumerable< FullPath > files, DirectoryInfo basePath, ModMeta modMeta )
    {
        DefaultData.Clear();
        GroupData.Clear();
        Count = 0;
        foreach( var file in files )
        {
            var metaData = file.Extension.ToLowerInvariant() switch
            {
                ".meta" => new TexToolsMeta( File.ReadAllBytes( file.FullName ) ),
                ".rgsp" => TexToolsMeta.FromRgspFile( file.FullName, File.ReadAllBytes( file.FullName ) ),
                _       => TexToolsMeta.Invalid,
            };

            if( metaData.FilePath == string.Empty )
            {
                continue;
            }

            Utf8RelPath.FromFile( file, basePath, out var path );
            var foundAny = false;
            foreach( var (name, group) in modMeta.Groups )
            {
                foreach( var option in group.Options.Where( o => o.OptionFiles.ContainsKey( path ) ) )
                {
                    foundAny = true;
                    AddMeta( name, option.OptionName, metaData );
                }
            }

            if( !foundAny )
            {
                AddMeta( string.Empty, string.Empty, metaData );
            }
        }

        SortLists();
    }

    public static FileInfo FileName( DirectoryInfo basePath )
        => new(Path.Combine( basePath.FullName, "metadata_manipulations.json" ));

    public void SaveToFile( FileInfo file )
    {
        try
        {
            var text = JsonConvert.SerializeObject( this, Formatting.Indented );
            File.WriteAllText( file.FullName, text );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not write metadata manipulations file to {file.FullName}:\n{e}" );
        }
    }

    public static MetaCollection? LoadFromFile( FileInfo file )
    {
        if( !file.Exists )
        {
            return null;
        }

        try
        {
            var text = File.ReadAllText( file.FullName );

            var collection = JsonConvert.DeserializeObject< MetaCollection >( text,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore } );

            if( collection != null )
            {
                if( collection.DefaultData.Concat( collection.GroupData.Values.SelectMany( kvp => kvp.Values.SelectMany( l => l ) ) )
                  .Any( m => m.ManipulationType == MetaManipulation.Type.Unknown || !Enum.IsDefined( m.ManipulationType ) ) )
                {
                    throw new Exception( "Invalid collection" );
                }

                collection.Count = collection.DefaultData.Count
                  + collection.GroupData.Values.SelectMany( kvp => kvp.Values ).Sum( l => l.Count );
            }

            return collection;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not load mod metadata manipulations from {file.FullName}:\n{e}" );
            return null;
        }
    }
}