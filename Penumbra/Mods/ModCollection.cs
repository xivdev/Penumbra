using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Util;
using Penumbra.Mod;
using Penumbra.Util;

namespace Penumbra.Mods;

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

    public void CalculateEffectiveFileList( bool withMetaManipulations, bool activeCollection )
    {
        PluginLog.Debug( "Recalculating effective file list for {CollectionName} [{WithMetaManipulations}] [{IsActiveCollection}]", Name,
            withMetaManipulations, activeCollection );
        Cache ??= new ModCollectionCache( this );
        UpdateSettings( false );
        Cache.CalculateEffectiveFileList();
        if( withMetaManipulations )
        {
            Cache.UpdateMetaManipulations();
            if( activeCollection )
            {
                Penumbra.ResidentResources.Reload();
            }
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

    public static readonly ModCollection Empty = new() { Name = "" };
}