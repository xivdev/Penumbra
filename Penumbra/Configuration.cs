using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Configuration;
using Dalamud.Logging;
using OtterGui.Classes;
using OtterGui.Filesystem;
using Penumbra.Import;
using Penumbra.UI.Classes;

namespace Penumbra;

[Serializable]
public partial class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = Constants.CurrentVersion;

    public bool EnableMods { get; set; } = true;
    public string ModDirectory { get; set; } = string.Empty;

    public bool HideUiInGPose { get; set; } = false;
    public bool HideUiInCutscenes { get; set; } = true;
    public bool HideUiWhenUiHidden { get; set; } = false;

    public bool UseCharacterCollectionInMainWindow { get; set; } = true;
    public bool UseCharacterCollectionsInCards { get; set; } = true;
    public bool UseCharacterCollectionInInspect { get; set; } = true;
    public bool UseCharacterCollectionInTryOn { get; set; } = true;
    public bool UseOwnerNameForCharacterCollection { get; set; } = true;
    public bool PreferNamedCollectionsOverOwners { get; set; } = true;
    public bool UseDefaultCollectionForRetainers { get; set; } = false;

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
    public bool OpenFoldersByDefault { get; set; } = false;
    public string DefaultImportFolder { get; set; } = string.Empty;
    public DoubleModifier DeleteModModifier { get; set; } = new(ModifierHotkey.Control, ModifierHotkey.Shift);

    public bool FixMainWindow { get; set; } = false;
    public bool ShowAdvanced { get; set; }
    public bool AutoDeduplicateOnImport { get; set; } = false;
    public bool EnableHttpApi { get; set; }

    public string DefaultModImportPath { get; set; } = string.Empty;
    public bool AlwaysOpenDefaultImport { get; set; } = false;
    public string DefaultModAuthor { get; set; } = DefaultTexToolsData.Author;

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
    private void SaveConfiguration()
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

    public void Save()
        => Penumbra.Framework.RegisterDelayed( nameof( SaveConfiguration ), SaveConfiguration );

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

    // Contains some default values or boundaries for config values.
    public static class Constants
    {
        public const int   CurrentVersion      = 3;
        public const float MaxAbsoluteSize     = 600;
        public const int   DefaultAbsoluteSize = 250;
        public const float MinAbsoluteSize     = 50;
        public const int   MaxScaledSize       = 80;
        public const int   DefaultScaledSize   = 20;
        public const int   MinScaledSize       = 5;
    }
}