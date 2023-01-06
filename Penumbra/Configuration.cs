using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Configuration;
using Newtonsoft.Json;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Filesystem;
using OtterGui.Widgets;
using Penumbra.Import;
using Penumbra.Mods;
using Penumbra.UI;
using Penumbra.UI.Classes;
using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;

namespace Penumbra;

[Serializable]
public partial class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = Constants.CurrentVersion;

    public int LastSeenVersion { get; set; } = ConfigWindow.LastChangelogVersion;
    public ChangeLogDisplayType ChangeLogDisplayType { get; set; } = ChangeLogDisplayType.New;

    public bool EnableMods { get; set; } = true;
    public string ModDirectory { get; set; } = string.Empty;
    public string ExportDirectory { get; set; } = string.Empty;

    public bool HideUiInGPose { get; set; } = false;
    public bool HideUiInCutscenes { get; set; } = true;
    public bool HideUiWhenUiHidden { get; set; } = false;

    public bool UseCharacterCollectionInMainWindow { get; set; } = true;
    public bool UseCharacterCollectionsInCards { get; set; } = true;
    public bool UseCharacterCollectionInInspect { get; set; } = true;
    public bool UseCharacterCollectionInTryOn { get; set; } = true;
    public bool UseOwnerNameForCharacterCollection { get; set; } = true;
    public bool UseNoModsInInspect { get; set; } = false;

    public bool HideRedrawBar { get; set; } = false;

#if DEBUG
    public bool DebugMode { get; set; } = true;
#else
    public bool DebugMode { get; set; } = false;
#endif

    public int TutorialStep { get; set; } = 0;

    public bool EnableFullResourceLogging { get; set; } = false;
    public bool EnableResourceLogging { get; set; } = false;
    public string ResourceLoggingFilter { get; set; } = string.Empty;

    [JsonConverter( typeof( SortModeConverter ) )]
    [JsonProperty( Order = int.MaxValue )]
    public ISortMode< Mod > SortMode = ISortMode< Mod >.FoldersFirst;

    public bool ScaleModSelector { get; set; } = false;
    public float ModSelectorAbsoluteSize { get; set; } = Constants.DefaultAbsoluteSize;
    public int ModSelectorScaledSize { get; set; } = Constants.DefaultScaledSize;
    public bool OpenFoldersByDefault { get; set; } = false;
    public string DefaultImportFolder { get; set; } = string.Empty;
    public DoubleModifier DeleteModModifier { get; set; } = new(ModifierHotkey.Control, ModifierHotkey.Shift);

    public bool FixMainWindow { get; set; } = false;
    public bool AutoDeduplicateOnImport { get; set; } = true;
    public bool EnableHttpApi { get; set; } = true;

    public string DefaultModImportPath { get; set; } = string.Empty;
    public bool AlwaysOpenDefaultImport { get; set; } = false;
    public bool KeepDefaultMetaChanges { get; set; } = false;
    public string DefaultModAuthor { get; set; } = DefaultTexToolsData.Author;

    public Dictionary< ColorId, uint > Colors { get; set; }
        = Enum.GetValues< ColorId >().ToDictionary( c => c, c => c.Data().DefaultColor );

    // Load the current configuration.
    // Includes adding new colors and migrating from old versions.
    public static Configuration Load()
    {
        void HandleDeserializationError( object? sender, ErrorEventArgs errorArgs )
        {
            Penumbra.Log.Error(
                $"Error parsing Configuration at {errorArgs.ErrorContext.Path}, using default or migrating:\n{errorArgs.ErrorContext.Error}" );
            errorArgs.ErrorContext.Handled = true;
        }

        Configuration? configuration = null;
        if( File.Exists( Dalamud.PluginInterface.ConfigFile.FullName ) )
        {
            var text = File.ReadAllText( Dalamud.PluginInterface.ConfigFile.FullName );
            configuration = JsonConvert.DeserializeObject< Configuration >( text, new JsonSerializerSettings
            {
                Error = HandleDeserializationError,
            } );
        }

        configuration ??= new Configuration();
        if( configuration.Version == Constants.CurrentVersion )
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
            var text = JsonConvert.SerializeObject( this, Formatting.Indented );
            File.WriteAllText( Dalamud.PluginInterface.ConfigFile.FullName, text );
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Could not save plugin configuration:\n{e}" );
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
        public const int   CurrentVersion      = 7;
        public const float MaxAbsoluteSize     = 600;
        public const int   DefaultAbsoluteSize = 250;
        public const float MinAbsoluteSize     = 50;
        public const int   MaxScaledSize       = 80;
        public const int   DefaultScaledSize   = 20;
        public const int   MinScaledSize       = 5;

        public static readonly ISortMode< Mod >[] ValidSortModes =
        {
            ISortMode< Mod >.FoldersFirst,
            ISortMode< Mod >.Lexicographical,
            new ModFileSystem.ImportDate(),
            new ModFileSystem.InverseImportDate(),
            ISortMode< Mod >.InverseFoldersFirst,
            ISortMode< Mod >.InverseLexicographical,
            ISortMode< Mod >.FoldersLast,
            ISortMode< Mod >.InverseFoldersLast,
            ISortMode< Mod >.InternalOrder,
            ISortMode< Mod >.InverseInternalOrder,
        };
    }

    private class SortModeConverter : JsonConverter< ISortMode< Mod > >
    {
        public override void WriteJson( JsonWriter writer, ISortMode< Mod >? value, JsonSerializer serializer )
        {
            value ??= ISortMode< Mod >.FoldersFirst;
            serializer.Serialize( writer, value.GetType().Name );
        }

        public override ISortMode< Mod > ReadJson( JsonReader reader, Type objectType, ISortMode< Mod >? existingValue,
            bool hasExistingValue,
            JsonSerializer serializer )
        {
            var name = serializer.Deserialize< string >( reader );
            if( name == null || !Constants.ValidSortModes.FindFirst( s => s.GetType().Name == name, out var mode ) )
            {
                return existingValue ?? ISortMode< Mod >.FoldersFirst;
            }

            return mode;
        }
    }
}