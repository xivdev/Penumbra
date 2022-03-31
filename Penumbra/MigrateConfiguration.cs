using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui.Filesystem;
using Penumbra.Collections;
using Penumbra.Mods;

namespace Penumbra;

public class MigrateConfiguration
{
    private Configuration _config = null!;
    private JObject       _data   = null!;

    public string                       CurrentCollection    = ModCollection.DefaultCollection;
    public string                       DefaultCollection    = ModCollection.DefaultCollection;
    public string                       ForcedCollection     = string.Empty;
    public Dictionary< string, string > CharacterCollections = new();
    public Dictionary< string, string > ModSortOrder         = new();
    public bool                         InvertModListOrder   = false;


    public static void Migrate( Configuration config )
    {
        var m = new MigrateConfiguration
        {
            _config = config,
            _data   = JObject.Parse( File.ReadAllText( Dalamud.PluginInterface.ConfigFile.FullName ) ),
        };

        m.CreateBackup();
        m.Version0To1();
        m.Version1To2();
    }

    private void Version1To2()
    {
        if( _config.Version != 1 )
        {
            return;
        }

        ResettleSortOrder();
        ResettleCollectionSettings();
        ResettleForcedCollection();
        _config.Version = 2;
    }

    private void ResettleForcedCollection()
    {
        ForcedCollection = _data[ nameof( ForcedCollection ) ]?.ToObject< string >() ?? ForcedCollection;
        if( ForcedCollection.Length <= 0 )
        {
            return;
        }

        foreach( var collection in Directory.EnumerateFiles( ModCollection.CollectionDirectory, "*.json" ) )
        {
            try
            {
                var jObject = JObject.Parse( File.ReadAllText( collection ) );
                if( jObject[ nameof( ModCollection.Name ) ]?.ToObject< string >() != ForcedCollection )
                {
                    jObject[ nameof( ModCollection.Inheritance ) ] = JToken.FromObject( new List< string >() { ForcedCollection } );
                    File.WriteAllText( collection, jObject.ToString() );
                }
            }
            catch( Exception e )
            {
                PluginLog.Error(
                    $"Could not transfer forced collection {ForcedCollection} to inheritance of collection {collection}:\n{e}" );
            }
        }
    }

    private void ResettleSortOrder()
    {
        ModSortOrder = _data[ nameof( ModSortOrder ) ]?.ToObject< Dictionary< string, string > >() ?? ModSortOrder;
        var       file   = Mod2.Manager.ModFileSystemFile;
        using var stream = File.Open( file, File.Exists( file ) ? FileMode.Truncate : FileMode.CreateNew );
        using var writer = new StreamWriter( stream );
        using var j      = new JsonTextWriter( writer );
        j.Formatting = Formatting.Indented;
        j.WriteStartObject();
        j.WritePropertyName( "Data" );
        j.WriteStartObject();
        foreach( var (mod, path) in ModSortOrder )
        {
            j.WritePropertyName( mod, true );
            j.WriteValue( path );
        }

        j.WriteEndObject();
        j.WritePropertyName( "EmptyFolders" );
        j.WriteStartArray();
        j.WriteEndArray();
        j.WriteEndObject();
    }

    private void ResettleCollectionSettings()
    {
        CurrentCollection    = _data[ nameof( CurrentCollection ) ]?.ToObject< string >()                          ?? CurrentCollection;
        DefaultCollection    = _data[ nameof( DefaultCollection ) ]?.ToObject< string >()                          ?? DefaultCollection;
        CharacterCollections = _data[ nameof( CharacterCollections ) ]?.ToObject< Dictionary< string, string > >() ?? CharacterCollections;
        ModCollection.Manager.SaveActiveCollections( DefaultCollection, CurrentCollection,
            CharacterCollections.Select( kvp => ( kvp.Key, kvp.Value ) ) );
    }

    private void Version0To1()
    {
        if( _config.Version != 0 )
        {
            return;
        }

        _config.ModDirectory = _data[ nameof( CurrentCollection ) ]?.ToObject< string >() ?? string.Empty;
        _config.Version      = 1;
        ResettleCollectionJson();
    }

    private void ResettleCollectionJson()
    {
        var collectionJson = new FileInfo( Path.Combine( _config.ModDirectory, "collection.json" ) );
        if( !collectionJson.Exists )
        {
            return;
        }

        var defaultCollection     = ModCollection.CreateNewEmpty( ModCollection.DefaultCollection );
        var defaultCollectionFile = defaultCollection.FileName;
        if( defaultCollectionFile.Exists )
        {
            return;
        }

        try
        {
            var text = File.ReadAllText( collectionJson.FullName );
            var data = JArray.Parse( text );

            var maxPriority = 0;
            var dict        = new Dictionary< string, ModSettings >();
            foreach( var setting in data.Cast< JObject >() )
            {
                var modName  = ( string )setting[ "FolderName" ]!;
                var enabled  = ( bool )setting[ "Enabled" ]!;
                var priority = ( int )setting[ "Priority" ]!;
                var settings = setting[ "Settings" ]!.ToObject< Dictionary< string, int > >()
                 ?? setting[ "Conf" ]!.ToObject< Dictionary< string, int > >();

                dict[ modName ] = new ModSettings()
                {
                    Enabled  = enabled,
                    Priority = priority,
                    Settings = settings!,
                };
                ;
                maxPriority = Math.Max( maxPriority, priority );
            }

            InvertModListOrder = _data[ nameof( InvertModListOrder ) ]?.ToObject< bool >() ?? InvertModListOrder;
            if( !InvertModListOrder )
            {
                foreach( var setting in dict.Values )
                {
                    setting.Priority = maxPriority - setting.Priority;
                }
            }

            defaultCollection = ModCollection.MigrateFromV0( ModCollection.DefaultCollection, dict );
            defaultCollection.Save();
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not migrate the old collection file to new collection files:\n{e}" );
            throw;
        }
    }

    private void CreateBackup()
    {
        var name    = Dalamud.PluginInterface.ConfigFile.FullName;
        var bakName = name + ".bak";
        try
        {
            File.Copy( name, bakName, true );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not create backup copy of config at {bakName}:\n{e}" );
        }
    }
}