using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Logging;

namespace Penumbra
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        private const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;

        public bool IsEnabled { get; set; } = true;

        public bool ScaleModSelector { get; set; } = false;
        public bool ShowAdvanced { get; set; }

        public bool DisableFileSystemNotifications { get; set; }

        public bool DisableSoundStreaming { get; set; } = true;
        public bool EnableHttpApi { get; set; }
        public bool EnablePlayerWatch { get; set; } = false;
        public int WaitFrames { get; set; } = 30;

        public string ModDirectory { get; set; } = string.Empty;
        public string TempDirectory { get; set; } = string.Empty;

        public string CurrentCollection { get; set; } = "Default";
        public string DefaultCollection { get; set; } = "Default";
        public string ForcedCollection { get; set; } = "";

        public bool SortFoldersFirst { get; set; } = false;
        public bool HasReadCharacterCollectionDesc { get; set; } = false;

        public Dictionary< string, string > CharacterCollections { get; set; } = new();
        public Dictionary< string, string > ModSortOrder { get; set; } = new();

        public bool InvertModListOrder { internal get; set; }
        public Vector2 ManageModsButtonOffset { get; set; } = 50 * Vector2.One;

        public static Configuration Load()
        {
            var configuration = Dalamud.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            if( configuration.Version == CurrentVersion )
            {
                return configuration;
            }

            MigrateConfiguration.Version0To1( configuration );
            configuration.Save();

            return configuration;
        }

        public void Save()
        {
            try
            {
                Dalamud.PluginInterface.SavePluginConfig( this );
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Could not save plugin configuration:\n{e}" );
            }
        }
    }
}