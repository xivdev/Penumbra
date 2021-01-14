using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Plugin;
using Newtonsoft.Json;
using Penumbra.Models;

namespace Penumbra.Mods
{
    public class ModCollection
    {
        private readonly DirectoryInfo _basePath;

        public List< ModInfo > ModSettings { get; set; }
        public ResourceMod[] EnabledMods { get; set; }


        public ModCollection( DirectoryInfo basePath )
        {
            _basePath = basePath;
        }

        public void Load()
        {
            // find the collection json
            var collectionPath = Path.Combine( _basePath.FullName, "collection.json" );
            if( File.Exists( collectionPath ) )
            {
                try
                {
                    ModSettings = JsonConvert.DeserializeObject< List< ModInfo > >( File.ReadAllText( collectionPath ) );
                    ModSettings = ModSettings.OrderBy( x => x.Priority ).ToList();
                }
                catch( Exception e )
                {
                    PluginLog.Error( $"failed to read log collection information, failed path: {collectionPath}, err: {e.Message}" );
                }
            }

#if DEBUG
            if( ModSettings != null )
            {
                foreach( var ms in ModSettings )
                {
                    PluginLog.Information(
                        "mod: {ModName} Enabled: {Enabled} Priority: {Priority}",
                        ms.FolderName, ms.Enabled, ms.Priority
                    );
                }
            }
#endif

            ModSettings ??= new();
            var foundMods = new List< string >();

            foreach( var modDir in _basePath.EnumerateDirectories() )
            {
                var metaFile = modDir.EnumerateFiles().FirstOrDefault( f => f.Name == "meta.json" );

                if( metaFile == null )
                {
                    PluginLog.LogError( "mod meta is missing for resource mod: {ResourceModLocation}", modDir );
                    continue;
                }

                var meta = JsonConvert.DeserializeObject< ModMeta >( File.ReadAllText( metaFile.FullName ) );

                var mod = new ResourceMod
                {
                    Meta = meta,
                    ModBasePath = modDir
                };

                var modEntry = FindOrCreateModSettings( mod );
                foundMods.Add( modDir.Name );
                mod.RefreshModFiles();
            }

            // remove any mods from the collection we didn't find
            ModSettings = ModSettings.Where(
                x =>
                    foundMods.Any(
                        fm => string.Equals( x.FolderName, fm, StringComparison.InvariantCultureIgnoreCase )
                    )
            ).ToList();

            // if anything gets removed above, the priority ordering gets fucked, so we need to resort and reindex them otherwise BAD THINGS HAPPEN
            ModSettings = ModSettings.OrderBy( x => x.Priority ).ToList();
            var p = 0;
            foreach( var modSetting in ModSettings )
            {
                modSetting.Priority = p++;
            }

            // reorder the resourcemods list so we can just directly iterate
            EnabledMods = GetOrderedAndEnabledModList().ToArray();

            // write the collection metadata back to disk
            Save();
        }

        public void Save()
        {
            var collectionPath = Path.Combine( _basePath.FullName, "collection.json" );

            try
            {
                var data = JsonConvert.SerializeObject( ModSettings.OrderBy( x => x.Priority ).ToList() );
                File.WriteAllText( collectionPath, data );
            }
            catch( Exception e )
            {
                PluginLog.Error( $"failed to write log collection information, failed path: {collectionPath}, err: {e.Message}" );
            }
        }

        public void ReorderMod( ModInfo info, bool up )
        {
            // todo: certified fucked tier

            var prio = info.Priority;
            var swapPrio = up ? prio + 1 : prio - 1;
            var swapMeta = ModSettings.FirstOrDefault( x => x.Priority == swapPrio );

            if( swapMeta == null )
            {
                return;
            }

            info.Priority = swapPrio;
            swapMeta.Priority = prio;

            // reorder mods list
            ModSettings = ModSettings.OrderBy( x => x.Priority ).ToList();
            EnabledMods = GetOrderedAndEnabledModList().ToArray();

            // save new prios
            Save();
        }


        public ModInfo FindModSettings( string name )
        {
            var settings = ModSettings.FirstOrDefault(
                x => string.Equals( x.FolderName, name, StringComparison.InvariantCultureIgnoreCase )
            );
#if DEBUG
            PluginLog.Information( "finding mod {ModName} - found: {ModSettingsExist}", name, settings != null );
#endif
            return settings;
        }

        public ModInfo AddModSettings( ResourceMod mod )
        {
            var entry = new ModInfo
            {
                Priority = ModSettings.Count,
                CurrentGroup = 0,
                CurrentTop = 0,
                CurrentBottom = 0,
                FolderName = mod.ModBasePath.Name,
                Enabled = true,
                Mod = mod
            };

#if DEBUG
            PluginLog.Information( "creating mod settings {ModName}", entry.FolderName );
#endif

            ModSettings.Add( entry );
            return entry;
        }

        public ModInfo FindOrCreateModSettings( ResourceMod mod )
        {
            var settings = FindModSettings( mod.ModBasePath.Name );
            if( settings != null )
            {
                settings.Mod = mod;
                return settings;
            }

            return AddModSettings( mod );
        }

        public IEnumerable<ModInfo> GetOrderedAndEnabledModSettings()
        {
            return ModSettings
                .Where( x => x.Enabled )
                .OrderBy( x => x.Priority );
        }

        public IEnumerable<ResourceMod> GetOrderedAndEnabledModList()
        {
            return GetOrderedAndEnabledModSettings()
                .Select( x => x.Mod );
        }

        public IEnumerable<(ResourceMod, ModInfo)> GetOrderedAndEnabledModListWithSettings()
        {
            return GetOrderedAndEnabledModSettings()
                .Select( x => (x.Mod, x) );
        }
    }
}