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

        public List< ModInfo >? ModSettings { get; set; }
        public ResourceMod[]? EnabledMods { get; set; }


        public ModCollection( DirectoryInfo basePath )
            => _basePath = basePath;

        public void Load( bool invertOrder = false )
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
                    PluginLog.Debug(
                        "mod: {ModName} Enabled: {Enabled} Priority: {Priority}",
                        ms.FolderName, ms.Enabled, ms.Priority
                    );
                }
            }
#endif

            ModSettings ??= new List< ModInfo >();
            var foundMods = new List< string >();

            foreach( var modDir in _basePath.EnumerateDirectories() )
            {
                if( modDir.Name.ToLowerInvariant() == MetaManager.TmpDirectory )
                {
                    continue;
                }

                var metaFile = modDir.EnumerateFiles().FirstOrDefault( f => f.Name == "meta.json" );

                if( metaFile == null )
                {
#if DEBUG
                    PluginLog.Error( "mod meta is missing for resource mod: {ResourceModLocation}", modDir );
#else
                    PluginLog.Debug( "mod meta is missing for resource mod: {ResourceModLocation}", modDir );
#endif
                    continue;
                }

                var meta = ModMeta.LoadFromFile( metaFile.FullName ) ?? new ModMeta();

                var mod = new ResourceMod( meta, modDir );
                FindOrCreateModSettings( mod );
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
            EnabledMods = GetOrderedAndEnabledModList( invertOrder ).ToArray();

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

        private int CleanPriority( int priority )
            => priority < 0 ? 0 : priority >= ModSettings!.Count ? ModSettings.Count - 1 : priority;

        public void ReorderMod( ModInfo info, int newPriority )
        {
            if( ModSettings == null )
            {
                return;
            }

            var oldPriority = info.Priority;
            newPriority = CleanPriority( newPriority );
            if( oldPriority == newPriority )
            {
                return;
            }

            info.Priority = newPriority;
            if( newPriority < oldPriority )
            {
                for( var i = oldPriority - 1; i >= newPriority; --i )
                {
                    ++ModSettings![ i ].Priority;
                    ModSettings.Swap( i, i + 1 );
                }
            }
            else
            {
                for( var i = oldPriority + 1; i <= newPriority; ++i )
                {
                    --ModSettings![ i ].Priority;
                    ModSettings.Swap( i - 1, i );
                }
            }

            EnabledMods = GetOrderedAndEnabledModList().ToArray();
            Save();
        }

        public void ReorderMod( ModInfo info, bool up )
            => ReorderMod( info, info.Priority + ( up ? 1 : -1 ) );

        public ModInfo? FindModSettings( string name )
        {
            var settings = ModSettings?.FirstOrDefault(
                x => string.Equals( x.FolderName, name, StringComparison.InvariantCultureIgnoreCase )
            );
#if DEBUG
            PluginLog.Information( "finding mod {ModName} - found: {ModSettingsExist}", name, settings != null );
#endif
            return settings;
        }

        public ModInfo AddModSettings( ResourceMod mod )
        {
            var entry = new ModInfo( mod )
            {
                Priority   = ModSettings?.Count ?? 0,
                FolderName = mod.ModBasePath.Name,
                Enabled    = true,
            };
            entry.FixInvalidSettings();
#if DEBUG
            PluginLog.Information( "creating mod settings {ModName}", entry.FolderName );
#endif
            ModSettings ??= new List< ModInfo >();
            ModSettings.Add( entry );
            return entry;
        }

        public ModInfo FindOrCreateModSettings( ResourceMod mod )
        {
            var settings = FindModSettings( mod.ModBasePath.Name );
            if( settings == null )
            {
                return AddModSettings( mod );
            }

            settings.Mod = mod;
            settings.FixInvalidSettings();
            return settings;
        }

        public IEnumerable< ModInfo > GetOrderedAndEnabledModSettings( bool invertOrder = false )
        {
            var query = ModSettings?
                   .Where( x => x.Enabled )
             ?? Enumerable.Empty< ModInfo >();

            if( !invertOrder )
            {
                return query.OrderBy( x => x.Priority );
            }

            return query.OrderByDescending( x => x.Priority );
        }

        public IEnumerable< ResourceMod > GetOrderedAndEnabledModList( bool invertOrder = false )
        {
            return GetOrderedAndEnabledModSettings( invertOrder )
               .Select( x => x.Mod );
        }

        public IEnumerable< (ResourceMod, ModInfo) > GetOrderedAndEnabledModListWithSettings( bool invertOrder = false )
        {
            return GetOrderedAndEnabledModSettings( invertOrder )
               .Select( x => ( x.Mod, x ) );
        }
    }
}