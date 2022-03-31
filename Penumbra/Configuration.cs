using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Logging;

namespace Penumbra;

[Serializable]
public partial class Configuration : IPluginConfiguration
{
    private const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;

    public bool EnableMods { get; set; } = true;
#if DEBUG
    public bool DebugMode { get; set; } = true;
#else
    public bool DebugMode { get; set; } = false;
#endif

    public bool EnableFullResourceLogging { get; set; } = false;
    public bool EnableResourceLogging { get; set; } = false;
    public string ResourceLoggingFilter { get; set; } = string.Empty;

    public bool ScaleModSelector { get; set; } = false;

    public bool ShowAdvanced { get; set; }

    public bool DisableFileSystemNotifications { get; set; }

    public bool DisableSoundStreaming { get; set; } = true;
    public bool EnableHttpApi { get; set; }

    public string ModDirectory { get; set; } = string.Empty;

    public bool SortFoldersFirst { get; set; } = false;
    public bool HasReadCharacterCollectionDesc { get; set; } = false;

    public static Configuration Load()
    {
        var iConfiguration = Dalamud.PluginInterface.GetPluginConfig();
        var configuration  = iConfiguration as Configuration ?? new Configuration();
        if( iConfiguration is { Version: CurrentVersion } )
        {
            return configuration;
        }

        MigrateConfiguration.Migrate( configuration );
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