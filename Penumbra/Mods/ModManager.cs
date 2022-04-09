using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.ByteString;
using Penumbra.Meta;
using Penumbra.Util;

namespace Penumbra.Mods;

public partial class Mod
{
    public enum ChangeType
    {
        Added,
        Removed,
        Changed,
    }

    // The ModManager handles the basic mods installed to the mod directory.
    // It also contains the CollectionManager that handles all collections.
    public class Manager : IEnumerable< Mod >
    {
        public DirectoryInfo BasePath { get; private set; } = null!;

        private readonly List< Mod > _mods = new();

        public Mod this[ int idx ]
            => _mods[ idx ];

        public IReadOnlyList< Mod > Mods
            => _mods;

        public IEnumerator< Mod > GetEnumerator()
            => _mods.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        public ModFolder StructuredMods { get; } = ModFileSystem.Root;

        public delegate void ModChangeDelegate( ChangeType type, Mod mod );

        public event ModChangeDelegate? ModChange;
        public event Action? ModDiscoveryStarted;
        public event Action? ModDiscoveryFinished;

        public bool Valid { get; private set; }

        public int Count
            => _mods.Count;

        public Configuration Config
            => Penumbra.Config;

        public void DiscoverMods( string newDir )
        {
            SetBaseDirectory( newDir, false );
            DiscoverMods();
        }

        private void SetBaseDirectory( string newPath, bool firstTime )
        {
            if( !firstTime && string.Equals( newPath, Config.ModDirectory, StringComparison.InvariantCultureIgnoreCase ) )
            {
                return;
            }

            if( newPath.Length == 0 )
            {
                Valid    = false;
                BasePath = new DirectoryInfo( "." );
            }
            else
            {
                var newDir = new DirectoryInfo( newPath );
                if( !newDir.Exists )
                {
                    try
                    {
                        Directory.CreateDirectory( newDir.FullName );
                        newDir.Refresh();
                    }
                    catch( Exception e )
                    {
                        PluginLog.Error( $"Could not create specified mod directory {newDir.FullName}:\n{e}" );
                    }
                }

                if( !firstTime )
                {
                    HandleSortOrderFiles( newDir );
                }

                BasePath = newDir;

                Valid = true;
                if( Config.ModDirectory != BasePath.FullName )
                {
                    Config.ModDirectory = BasePath.FullName;
                    Config.Save();
                }
            }
        }

        private const string SortOrderFileName = "sort_order.json";
        public static string SortOrderFile     = Path.Combine( Dalamud.PluginInterface.GetPluginConfigDirectory(), SortOrderFileName );

        private void HandleSortOrderFiles( DirectoryInfo newDir )
        {
            try
            {
                var mainFile = SortOrderFile;
                // Copy old sort order to backup.
                var oldSortOrderFile = Path.Combine( BasePath.FullName, SortOrderFileName );
                PluginLog.Debug( "Copying current sort older file to {BackupFile}...", oldSortOrderFile );
                File.Copy( mainFile, oldSortOrderFile, true );
                BasePath = newDir;
                var newSortOrderFile = Path.Combine( newDir.FullName, SortOrderFileName );
                // Copy new sort order to main, if it exists.
                if( File.Exists( newSortOrderFile ) )
                {
                    File.Copy( newSortOrderFile, mainFile, true );
                    PluginLog.Debug( "Copying stored sort order file from {BackupFile}...", newSortOrderFile );
                }
                else
                {
                    File.Delete( mainFile );
                    PluginLog.Debug( "Deleting current sort order file...", newSortOrderFile );
                }
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Could not swap Sort Order files:\n{e}" );
            }
        }

        public Manager()
        {
            SetBaseDirectory( Config.ModDirectory, true );
            // TODO
            try
            {
                var data = JObject.Parse( File.ReadAllText( SortOrderFile ) );
                TemporaryModSortOrder = data[ "Data" ]?.ToObject< Dictionary< string, string > >() ?? new Dictionary< string, string >();
            }
            catch
            {
                TemporaryModSortOrder = new Dictionary< string, string >();
            }
        }

        public Dictionary< string, string > TemporaryModSortOrder;

        private bool SetSortOrderPath( Mod mod, string path )
        {
            mod.Move( path );
            var fixedPath = mod.Order.FullPath;
            if( fixedPath.Length == 0 || string.Equals( fixedPath, mod.Meta.Name, StringComparison.InvariantCultureIgnoreCase ) )
            {
                Penumbra.ModManager.TemporaryModSortOrder.Remove( mod.BasePath.Name );
                return true;
            }

            if( path != fixedPath )
            {
                TemporaryModSortOrder[ mod.BasePath.Name ] = fixedPath;
                return true;
            }

            return false;
        }

        private void SetModStructure( bool removeOldPaths = false )
        {
            var changes = false;

            foreach( var (folder, path) in TemporaryModSortOrder.ToArray() )
            {
                if( path.Length > 0 && _mods.FindFirst( m => m.BasePath.Name == folder, out var mod ) )
                {
                    changes |= SetSortOrderPath( mod, path );
                }
                else if( removeOldPaths )
                {
                    changes = true;
                    TemporaryModSortOrder.Remove( folder );
                }
            }

            if( changes )
            {
                Config.Save();
            }
        }

        public void DiscoverMods()
        {
            ModDiscoveryStarted?.Invoke();
            _mods.Clear();
            BasePath.Refresh();

            StructuredMods.SubFolders.Clear();
            StructuredMods.Mods.Clear();
            if( Valid && BasePath.Exists )
            {
                foreach( var modFolder in BasePath.EnumerateDirectories() )
                {
                    var mod = LoadMod( StructuredMods, modFolder );
                    if( mod == null )
                    {
                        continue;
                    }

                    mod.Index = _mods.Count;
                    _mods.Add( mod );
                }

                SetModStructure();
            }

            ModDiscoveryFinished?.Invoke();
        }

        public void DeleteMod( DirectoryInfo modFolder )
        {
            if( Directory.Exists( modFolder.FullName ) )
            {
                try
                {
                    Directory.Delete( modFolder.FullName, true );
                }
                catch( Exception e )
                {
                    PluginLog.Error( $"Could not delete the mod {modFolder.Name}:\n{e}" );
                }
            }

            var idx = _mods.FindIndex( m => m.BasePath.Name == modFolder.Name );
            if( idx >= 0 )
            {
                var mod = _mods[ idx ];
                mod.Order.ParentFolder.RemoveMod( mod );
                _mods.RemoveAt( idx );
                for( var i = idx; i < _mods.Count; ++i )
                {
                    --_mods[ i ].Index;
                }

                ModChange?.Invoke( ChangeType.Removed, mod );
            }
        }

        public int AddMod( DirectoryInfo modFolder )
        {
            var mod = LoadMod( StructuredMods, modFolder );
            if( mod == null )
            {
                return -1;
            }

            if( TemporaryModSortOrder.TryGetValue( mod.BasePath.Name, out var sortOrder ) )
            {
                if( SetSortOrderPath( mod, sortOrder ) )
                {
                    Config.Save();
                }
            }

            if( _mods.Any( m => m.BasePath.Name == modFolder.Name ) )
            {
                return -1;
            }

            _mods.Add( mod );
            ModChange?.Invoke( ChangeType.Added, mod );

            return _mods.Count - 1;
        }

        public bool UpdateMod( int idx, bool reloadMeta = false, bool recomputeMeta = false, bool force = false )
        {
            var mod         = Mods[ idx ];
            var oldName     = mod.Meta.Name;
            var metaChanges = mod.Meta.RefreshFromFile( mod.MetaFile ) != 0 || force;
            var fileChanges = mod.Resources.RefreshModFiles( mod.BasePath );

            if( !recomputeMeta && !reloadMeta && !metaChanges && fileChanges == 0 )
            {
                return false;
            }

            if( metaChanges || fileChanges.HasFlag( ResourceChange.Files ) )
            {
                mod.ComputeChangedItems();
                if( TemporaryModSortOrder.TryGetValue( mod.BasePath.Name, out var sortOrder ) )
                {
                    mod.Move( sortOrder );
                    var path = mod.Order.FullPath;
                    if( path != sortOrder )
                    {
                        TemporaryModSortOrder[ mod.BasePath.Name ] = path;
                        Config.Save();
                    }
                }
                else
                {
                    mod.Order = new SortOrder( StructuredMods, mod.Meta.Name );
                }
            }

            var nameChange = !string.Equals( oldName, mod.Meta.Name, StringComparison.InvariantCulture );

            recomputeMeta |= fileChanges.HasFlag( ResourceChange.Meta );
            if( recomputeMeta )
            {
                mod.Resources.MetaManipulations.Update( mod.Resources.MetaFiles, mod.BasePath, mod.Meta );
                mod.Resources.MetaManipulations.SaveToFile( MetaCollection.FileName( mod.BasePath ) );
            }

            // TODO: more specific mod changes?
            ModChange?.Invoke( ChangeType.Changed, mod );
            return true;
        }

        public bool UpdateMod( Mod mod, bool reloadMeta = false, bool recomputeMeta = false, bool force = false )
            => UpdateMod( Mods.IndexOf( mod ), reloadMeta, recomputeMeta, force );

        public static FullPath? ResolvePath( Utf8GamePath gameResourcePath )
            => Penumbra.CollectionManager.Default.ResolvePath( gameResourcePath );
    }
}