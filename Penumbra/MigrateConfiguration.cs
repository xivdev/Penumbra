using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Plugin;
using Newtonsoft.Json.Linq;
using Penumbra.Mod;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra
{
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

            var defaultCollection     = new ModCollection();
            var defaultCollectionFile = defaultCollection.FileName();
            if( defaultCollectionFile.Exists )
            {
                return;
            }

            try
            {
                var text = File.ReadAllText( collectionJson.FullName );
                var data = JArray.Parse( text );

                var maxPriority = 0;
                foreach( var setting in data.Cast< JObject >() )
                {
                    var modName  = ( string )setting[ "FolderName" ]!;
                    var enabled  = ( bool )setting[ "Enabled" ]!;
                    var priority = ( int )setting[ "Priority" ]!;
                    var settings = setting[ "Settings" ]!.ToObject< Dictionary< string, int > >()
                     ?? setting[ "Conf" ]!.ToObject< Dictionary< string, int > >();

                    var save = new ModSettings()
                    {
                        Enabled  = enabled,
                        Priority = priority,
                        Settings = settings!,
                    };
                    defaultCollection.Settings.Add( modName, save );
                    maxPriority = Math.Max( maxPriority, priority );
                }

                if( !config.InvertModListOrder )
                {
                    foreach( var setting in defaultCollection.Settings.Values )
                    {
                        setting.Priority = maxPriority - setting.Priority;
                    }
                }

                defaultCollection.Save( Service< DalamudPluginInterface >.Get() );
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Could not migrate the old collection file to new collection files:\n{e}" );
                throw;
            }
        }
    }
}