using Dalamud.Plugin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Penumbra.GameData.Util;
using Penumbra.Interop;
using Penumbra.Mod;
using Penumbra.Util;

namespace Penumbra.Mods
{
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

        private bool CleanUnavailableSettings( Dictionary< string, ModData > data )
        {
            if( Settings.Count <= data.Count )
            {
                return false;
            }

            List< string > removeList = new();
            foreach( var settingKvp in Settings )
            {
                if( !data.ContainsKey( settingKvp.Key ) )
                {
                    removeList.Add( settingKvp.Key );
                }
            }

            foreach( var s in removeList )
            {
                Settings.Remove( s );
            }

            return removeList.Count > 0;
        }

        public void CreateCache( DirectoryInfo modDirectory, Dictionary< string, ModData > data, bool cleanUnavailable = false )
        {
            Cache = new ModCollectionCache( Name, modDirectory );
            var changedSettings = false;
            foreach( var modKvp in data )
            {
                if( Settings.TryGetValue( modKvp.Key, out var settings ) )
                {
                    Cache.AvailableMods.Add( new Mod.Mod( settings, modKvp.Value ) );
                }
                else
                {
                    changedSettings = true;
                    var newSettings = ModSettings.DefaultSettings( modKvp.Value.Meta );
                    Settings.Add( modKvp.Key, newSettings );
                    Cache.AvailableMods.Add( new Mod.Mod( newSettings, modKvp.Value ) );
                }
            }

            if( cleanUnavailable )
            {
                changedSettings |= CleanUnavailableSettings( data );
            }

            if( changedSettings )
            {
                Save( Service< DalamudPluginInterface >.Get() );
            }

            Cache.SortMods();
            CalculateEffectiveFileList( modDirectory, true, false );
        }

        public void ClearCache()
            => Cache = null;

        public void UpdateSetting( ModData mod )
        {
            if( !Settings.TryGetValue( mod.BasePath.Name, out var settings ) )
            {
                return;
            }

            if( settings.FixInvalidSettings( mod.Meta ) )
            {
                Save( Service< DalamudPluginInterface >.Get() );
            }
        }

        public void UpdateSettings()
        {
            if( Cache == null )
            {
                return;
            }

            var changes = false;
            foreach( var mod in Cache.AvailableMods )
            {
                changes |= mod.FixSettings();
            }

            if( changes )
            {
                Save( Service< DalamudPluginInterface >.Get() );
            }
        }

        public void CalculateEffectiveFileList( DirectoryInfo modDir, bool withMetaManipulations, bool activeCollection )
        {
            Cache ??= new ModCollectionCache( Name, modDir );
            UpdateSettings();
            Cache.CalculateEffectiveFileList();
            if( withMetaManipulations )
            {
                Cache.UpdateMetaManipulations();
                if( activeCollection )
                {
                    Service< GameResourceManagement >.Get().ReloadPlayerResources();
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

        public static DirectoryInfo CollectionDir( DalamudPluginInterface pi )
            => new( Path.Combine( pi.GetPluginConfigDirectory(), "collections" ) );

        private static FileInfo FileName( DirectoryInfo collectionDir, string name )
            => new( Path.Combine( collectionDir.FullName, $"{name.RemoveInvalidPathSymbols()}.json" ) );

        public FileInfo FileName()
            => new( Path.Combine( Service< DalamudPluginInterface >.Get().GetPluginConfigDirectory(),
                $"{Name.RemoveInvalidPathSymbols()}.json" ) );

        public void Save( DalamudPluginInterface pi )
        {
            try
            {
                var dir = CollectionDir( pi );
                dir.Create();
                var file = FileName( dir, Name );
                SaveToFile( file );
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Could not save collection {Name}:\n{e}" );
            }
        }

        public static ModCollection? Load( string name, DalamudPluginInterface pi )
        {
            var file = FileName( CollectionDir( pi ), name );
            return file.Exists ? LoadFromFile( file ) : null;
        }

        public void Delete( DalamudPluginInterface pi )
        {
            var file = FileName( CollectionDir( pi ), Name );
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

            if( Settings.TryGetValue( data.BasePath.Name, out var settings ) )
            {
                Cache.AddMod( settings, data );
            }
            else
            {
                Cache.AddMod( ModSettings.DefaultSettings( data.Meta ), data );
            }
        }

        public string? ResolveSwappedOrReplacementPath( GamePath gameResourcePath )
            => Cache?.ResolveSwappedOrReplacementPath( gameResourcePath );

        public static readonly ModCollection Empty = new() { Name = "" };
    }
}