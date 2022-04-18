using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Configuration;
using Dalamud.Logging;
using OtterGui.Filesystem;
using Penumbra.UI.Classes;

namespace Penumbra;

[Serializable]
public partial class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = Constants.CurrentVersion;

    public bool EnableMods { get; set; } = true;
    public string ModDirectory { get; set; } = string.Empty;


#if DEBUG
    public bool DebugMode { get; set; } = true;
#else
    public bool DebugMode { get; set; } = false;
#endif

    public bool EnableFullResourceLogging { get; set; } = false;
    public bool EnableResourceLogging { get; set; } = false;
    public string ResourceLoggingFilter { get; set; } = string.Empty;


    public SortMode SortMode { get; set; } = SortMode.FoldersFirst;
    public bool ScaleModSelector { get; set; } = false;
    public float ModSelectorAbsoluteSize { get; set; } = Constants.DefaultAbsoluteSize;
    public int ModSelectorScaledSize { get; set; } = Constants.DefaultScaledSize;


    public bool ShowAdvanced { get; set; }
    public bool DisableSoundStreaming { get; set; } = true;
    public bool EnableHttpApi { get; set; }

    public Dictionary< ColorId, uint > Colors { get; set; }
        = Enum.GetValues< ColorId >().ToDictionary( c => c, c => c.Data().DefaultColor );

    // Load the current configuration.
    // Includes adding new colors and migrating from old versions.
    public static Configuration Load()
    {
        var iConfiguration = Dalamud.PluginInterface.GetPluginConfig();
        var configuration  = iConfiguration as Configuration ?? new Configuration();
        if( iConfiguration is { Version: Constants.CurrentVersion } )
        {
            configuration.AddColors( false );
            return configuration;
        }

        Migration.Migrate( configuration );
        configuration.AddColors( true );

        return configuration;
    }

    // Save the current configuration.
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

    // Add missing colors to the dictionary if necessary.
    private void AddColors( bool forceSave )
    {
        var save = false;
        foreach( var color in Enum.GetValues< ColorId >() )
        {
            save |= Colors.TryAdd( color, color.Data().DefaultColor );
        }

        if( save || forceSave )
        {
            Save();
        }
    }
}