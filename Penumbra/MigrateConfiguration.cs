using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Newtonsoft.Json.Linq;
using Penumbra.Collections;
using Penumbra.Mod;
using Penumbra.Mods;

namespace Penumbra;

public static class MigrateConfiguration
{
    public static void Version0To1( Configuration config )
    {
        if( config.Version != 0 )
        {
            return;
        }

        config.ModDirectory      = config.CurrentCollection;
        config.CurrentCollection = "Default";
        config.DefaultCollection = "Default";
        config.Version           = 1;
        ResettleCollectionJson( config );
    }

    private static void ResettleCollectionJson( Configuration config )
    {
        var collectionJson = new FileInfo( Path.Combine( config.ModDirectory, "collection.json" ) );
        if( !collectionJson.Exists )
        {
            return;
        }

        var defaultCollection     = ModCollection2.CreateNewEmpty( ModCollection2.DefaultCollection );
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

            if( !config.InvertModListOrder )
            {
                foreach( var setting in dict.Values )
                {
                    setting.Priority = maxPriority - setting.Priority;
                }
            }

            defaultCollection = ModCollection2.MigrateFromV0( ModCollection2.DefaultCollection, dict );
            defaultCollection.Save();
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not migrate the old collection file to new collection files:\n{e}" );
            throw;
        }
    }
}