using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Dalamud.Logging;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.ByteString;
using Penumbra.Meta.Manager;
using Penumbra.Mod;
using Penumbra.Util;

namespace Penumbra.Mods;

public partial class ModCollection2
{
    public const int    CurrentVersion    = 1;
    public const string DefaultCollection = "Default";

    public string Name { get; private init; }
    public int Version { get; private set; }

    private readonly List< ModSettings? > _settings;

    public IReadOnlyList< ModSettings? > Settings
        => _settings;

    public IEnumerable< ModSettings? > ActualSettings
        => Enumerable.Range( 0, _settings.Count ).Select( i => this[ i ].Settings );

    private readonly Dictionary< string, ModSettings > _unusedSettings;

    private ModCollection2( string name, ModCollection2 duplicate )
    {
        Name               =  name;
        Version            =  duplicate.Version;
        _settings          =  duplicate._settings.ConvertAll( s => s?.DeepCopy() );
        _unusedSettings    =  duplicate._unusedSettings.ToDictionary( kvp => kvp.Key, kvp => kvp.Value.DeepCopy() );
        _inheritance       =  duplicate._inheritance.ToList();
        ModSettingChanged  += SaveOnChange;
        InheritanceChanged += Save;
    }

    private ModCollection2( string name, int version, Dictionary< string, ModSettings > allSettings )
    {
        Name            = name;
        Version         = version;
        _unusedSettings = allSettings;
        _settings       = Enumerable.Repeat( ( ModSettings? )null, Penumbra.ModManager.Count ).ToList();
        for( var i = 0; i < Penumbra.ModManager.Count; ++i )
        {
            var modName = Penumbra.ModManager[ i ].BasePath.Name;
            if( _unusedSettings.TryGetValue( Penumbra.ModManager[ i ].BasePath.Name, out var settings ) )
            {
                _unusedSettings.Remove( modName );
                _settings[ i ] = settings;
            }
        }

        Migration.Migrate( this );
        ModSettingChanged  += SaveOnChange;
        InheritanceChanged += Save;
    }

    public static ModCollection2 CreateNewEmpty( string name )
        => new(name, CurrentVersion, new Dictionary< string, ModSettings >());

    public ModCollection2 Duplicate( string name )
        => new(name, this);

    private void CleanUnavailableSettings()
    {
        var any = _unusedSettings.Count > 0;
        _unusedSettings.Clear();
        if( any )
        {
            Save();
        }
    }

    public void AddMod( ModData mod )
    {
        if( _unusedSettings.TryGetValue( mod.BasePath.Name, out var settings ) )
        {
            _settings.Add( settings );
            _unusedSettings.Remove( mod.BasePath.Name );
        }
        else
        {
            _settings.Add( null );
        }
    }

    public void RemoveMod( ModData mod, int idx )
    {
        var settings = _settings[ idx ];
        if( settings != null )
        {
            _unusedSettings.Add( mod.BasePath.Name, settings );
        }

        _settings.RemoveAt( idx );
    }

    public static string CollectionDirectory
        => Path.Combine( Dalamud.PluginInterface.GetPluginConfigDirectory(), "collections" );

    private FileInfo FileName
        => new(Path.Combine( CollectionDirectory, $"{Name.RemoveInvalidPathSymbols()}.json" ));

    public void Save()
    {
        try
        {
            var file = FileName;
            file.Directory?.Create();
            using var s = file.Open( FileMode.Truncate );
            using var w = new StreamWriter( s, Encoding.UTF8 );
            using var j = new JsonTextWriter( w );
            j.Formatting = Formatting.Indented;
            var x = JsonSerializer.Create( new JsonSerializerSettings { Formatting = Formatting.Indented } );
            j.WriteStartObject();
            j.WritePropertyName( nameof( Version ) );
            j.WriteValue( Version );
            j.WritePropertyName( nameof( Name ) );
            j.WriteValue( Name );
            j.WritePropertyName( nameof( Settings ) );
            j.WriteStartObject();
            for( var i = 0; i < _settings.Count; ++i )
            {
                var settings = _settings[ i ];
                if( settings != null )
                {
                    j.WritePropertyName( Penumbra.ModManager[ i ].BasePath.Name );
                    x.Serialize( j, settings );
                }
            }

            foreach( var settings in _unusedSettings )
            {
                j.WritePropertyName( settings.Key );
                x.Serialize( j, settings.Value );
            }

            j.WriteEndObject();
            j.WritePropertyName( nameof( Inheritance ) );
            x.Serialize( j, Inheritance );
            j.WriteEndObject();
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not save collection {Name}:\n{e}" );
        }
    }

    public void Delete()
    {
        var file = FileName;
        if( file.Exists )
        {
            try
            {
                file.Delete();
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Could not delete collection file {file.FullName} for {Name}:\n{e}" );
            }
        }
    }

    public static ModCollection2? LoadFromFile( FileInfo file, out IReadOnlyList< string > inheritance )
    {
        inheritance = Array.Empty< string >();
        if( !file.Exists )
        {
            PluginLog.Error( $"Could not read collection because {file.FullName} does not exist." );
            return null;
        }

        try
        {
            var obj     = JObject.Parse( File.ReadAllText( file.FullName ) );
            var name    = obj[ nameof( Name ) ]?.ToObject< string >() ?? string.Empty;
            var version = obj[ nameof( Version ) ]?.ToObject< int >() ?? 0;
            var settings = obj[ nameof( Settings ) ]?.ToObject< Dictionary< string, ModSettings > >()
             ?? new Dictionary< string, ModSettings >();
            inheritance = obj[ nameof( Inheritance ) ]?.ToObject< List< string > >() ?? ( IReadOnlyList< string > )Array.Empty< string >();

            return new ModCollection2( name, version, settings );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not read collection information from {file.FullName}:\n{e}" );
        }

        return null;
    }
}

// A ModCollection is a named set of ModSettings to all of the users' installed mods.
// It is meant to be local only, and thus should always contain settings for every mod, not just the enabled ones.
// Settings to mods that are not installed anymore are kept as long as no call to CleanUnavailableSettings is made.
// Active ModCollections build a cache of currently relevant data.
public class ModCollection
{
    public const string DefaultCollection = "Default";

    public string Name { get; set; }

    public Dictionary< string, ModSettings > Settings { get; }

    public ModCollection()
    {
        Name     = DefaultCollection;
        Settings = new Dictionary< string, ModSettings >();
    }

    public ModCollection( string name, Dictionary< string, ModSettings > settings )
    {
        Name     = name;
        Settings = settings.ToDictionary( kvp => kvp.Key, kvp => kvp.Value.DeepCopy() );
    }

    public Mod.Mod GetMod( ModData mod )
    {
        if( Cache != null && Cache.AvailableMods.TryGetValue( mod.BasePath.Name, out var ret ) )
        {
            return ret;
        }

        if( Settings.TryGetValue( mod.BasePath.Name, out var settings ) )
        {
            return new Mod.Mod( settings, mod );
        }

        var newSettings = ModSettings.DefaultSettings( mod.Meta );
        Settings.Add( mod.BasePath.Name, newSettings );
        Save();
        return new Mod.Mod( newSettings, mod );
    }

    private bool CleanUnavailableSettings( Dictionary< string, ModData > data )
    {
        var removeList = Settings.Where( settingKvp => !data.ContainsKey( settingKvp.Key ) ).ToArray();

        foreach( var s in removeList )
        {
            Settings.Remove( s.Key );
        }

        return removeList.Length > 0;
    }

    public void CreateCache( IEnumerable< ModData > data )
    {
        Cache = new ModCollectionCache( this );
        var changedSettings = false;
        foreach( var mod in data )
        {
            if( Settings.TryGetValue( mod.BasePath.Name, out var settings ) )
            {
                Cache.AddMod( settings, mod, false );
            }
            else
            {
                changedSettings = true;
                var newSettings = ModSettings.DefaultSettings( mod.Meta );
                Settings.Add( mod.BasePath.Name, newSettings );
                Cache.AddMod( newSettings, mod, false );
            }
        }

        if( changedSettings )
        {
            Save();
        }

        CalculateEffectiveFileList( true, false );
    }

    public void ClearCache()
        => Cache = null;

    public void UpdateSetting( DirectoryInfo modPath, ModMeta meta, bool clear )
    {
        if( !Settings.TryGetValue( modPath.Name, out var settings ) )
        {
            return;
        }

        if( clear )
        {
            settings.Settings.Clear();
        }

        if( settings.FixInvalidSettings( meta ) )
        {
            Save();
        }
    }

    public void UpdateSetting( ModData mod )
        => UpdateSetting( mod.BasePath, mod.Meta, false );

    public void UpdateSettings( bool forceSave )
    {
        if( Cache == null )
        {
            return;
        }

        var changes = false;
        foreach( var mod in Cache.AvailableMods.Values )
        {
            changes |= mod.FixSettings();
        }

        if( forceSave || changes )
        {
            Save();
        }
    }

    public void CalculateEffectiveFileList( bool withMetaManipulations, bool reloadResident )
    {
        PluginLog.Debug( "Recalculating effective file list for {CollectionName} [{WithMetaManipulations}]", Name, withMetaManipulations );
        Cache ??= new ModCollectionCache( this );
        UpdateSettings( false );
        Cache.CalculateEffectiveFileList();
        if( withMetaManipulations )
        {
            Cache.UpdateMetaManipulations();
        }

        if( reloadResident )
        {
            Penumbra.ResidentResources.Reload();
        }
    }


    [JsonIgnore]
    public ModCollectionCache? Cache { get; private set; }

    public static ModCollection? LoadFromFile( FileInfo file )
    {
        if( !file.Exists )
        {
            PluginLog.Error( $"Could not read collection because {file.FullName} does not exist." );
            return null;
        }

        try
        {
            var collection = JsonConvert.DeserializeObject< ModCollection >( File.ReadAllText( file.FullName ) );
            return collection;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not read collection information from {file.FullName}:\n{e}" );
        }

        return null;
    }

    private void SaveToFile( FileInfo file )
    {
        try
        {
            File.WriteAllText( file.FullName, JsonConvert.SerializeObject( this, Formatting.Indented ) );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not write collection {Name} to {file.FullName}:\n{e}" );
        }
    }

    public static DirectoryInfo CollectionDir()
        => new(Path.Combine( Dalamud.PluginInterface.GetPluginConfigDirectory(), "collections" ));

    private static FileInfo FileName( DirectoryInfo collectionDir, string name )
        => new(Path.Combine( collectionDir.FullName, $"{name.RemoveInvalidPathSymbols()}.json" ));

    public FileInfo FileName()
        => new(Path.Combine( Dalamud.PluginInterface.GetPluginConfigDirectory(),
            $"{Name.RemoveInvalidPathSymbols()}.json" ));

    public void Save()
    {
        try
        {
            var dir = CollectionDir();
            dir.Create();
            var file = FileName( dir, Name );
            SaveToFile( file );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not save collection {Name}:\n{e}" );
        }
    }

    public static ModCollection? Load( string name )
    {
        var file = FileName( CollectionDir(), name );
        return file.Exists ? LoadFromFile( file ) : null;
    }

    public void Delete()
    {
        var file = FileName( CollectionDir(), Name );
        if( file.Exists )
        {
            try
            {
                file.Delete();
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Could not delete collection file {file} for {Name}:\n{e}" );
            }
        }
    }

    public void AddMod( ModData data )
    {
        if( Cache == null )
        {
            return;
        }

        Cache.AddMod( Settings.TryGetValue( data.BasePath.Name, out var settings )
                ? settings
                : ModSettings.DefaultSettings( data.Meta ),
            data );
    }

    public FullPath? ResolveSwappedOrReplacementPath( Utf8GamePath gameResourcePath )
        => Cache?.ResolveSwappedOrReplacementPath( gameResourcePath );


    [Conditional( "USE_EQP" )]
    public void SetEqpFiles()
    {
        if( Cache == null )
        {
            MetaManager.MetaManagerEqp.ResetFiles();
        }
        else
        {
            Cache.MetaManipulations.Eqp.SetFiles();
        }
    }

    [Conditional( "USE_EQDP" )]
    public void SetEqdpFiles()
    {
        if( Cache == null )
        {
            MetaManager.MetaManagerEqdp.ResetFiles();
        }
        else
        {
            Cache.MetaManipulations.Eqdp.SetFiles();
        }
    }

    [Conditional( "USE_GMP" )]
    public void SetGmpFiles()
    {
        if( Cache == null )
        {
            MetaManager.MetaManagerGmp.ResetFiles();
        }
        else
        {
            Cache.MetaManipulations.Gmp.SetFiles();
        }
    }

    [Conditional( "USE_EST" )]
    public void SetEstFiles()
    {
        if( Cache == null )
        {
            MetaManager.MetaManagerEst.ResetFiles();
        }
        else
        {
            Cache.MetaManipulations.Est.SetFiles();
        }
    }

    [Conditional( "USE_CMP" )]
    public void SetCmpFiles()
    {
        if( Cache == null )
        {
            MetaManager.MetaManagerCmp.ResetFiles();
        }
        else
        {
            Cache.MetaManipulations.Cmp.SetFiles();
        }
    }

    public void SetFiles()
    {
        if( Cache == null )
        {
            Penumbra.CharacterUtility.ResetAll();
        }
        else
        {
            Cache.MetaManipulations.SetFiles();
        }
    }

    public static readonly ModCollection Empty = new() { Name = "" };
}