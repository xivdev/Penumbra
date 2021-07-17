using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Penumbra.Util;

namespace Penumbra
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        private const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;

        public bool IsEnabled { get; set; } = true;

        public bool ShowAdvanced { get; set; }

        public bool DisableFileSystemNotifications { get; set; }

        public bool EnableHttpApi { get; set; }
        public bool EnableActorWatch { get; set; } = false;

        public string ModDirectory { get; set; } = string.Empty;

        public string CurrentCollection { get; set; } = "Default";
        public string DefaultCollection { get; set; } = "Default";
        public string ForcedCollection { get; set; } = "";
        public Dictionary< string, string > CharacterCollections { get; set; } = new();
        public Dictionary< string, string > ModSortOrder { get; set; } = new();

        public bool InvertModListOrder { internal get; set; }

        public static Configuration Load( DalamudPluginInterface pi )
        {
            var configuration = pi.GetPluginConfig() as Configuration ?? new Configuration();
            if( configuration.Version == CurrentVersion )
            {
                return configuration;
            }

            MigrateConfiguration.Version0To1( configuration );
            configuration.Save( pi );

            return configuration;
        }

        public void Save( DalamudPluginInterface pi )
        {
            try
            {
                pi.SavePluginConfig( this );
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Could not save plugin configuration:\n{e}" );
            }
        }

        public void Save()
            => Save( Service< DalamudPluginInterface >.Get() );
    }
}