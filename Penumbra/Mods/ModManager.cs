using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Penumbra.GameData.ByteString;
using Penumbra.Meta;
using Penumbra.Mods;
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

        public delegate void ModChangeDelegate( ChangeType type, int modIndex, Mod mod );

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

                BasePath = newDir;
                Valid    = true;
                if( Config.ModDirectory != BasePath.FullName )
                {
                    Config.ModDirectory = BasePath.FullName;
                    Config.Save();
                }
            }
        }

        public Manager()
        {
            SetBaseDirectory( Config.ModDirectory, true );
        }

        private bool SetSortOrderPath( Mod mod, string path )
        {
            mod.Move( path );
            var fixedPath = mod.Order.FullPath;
            if( fixedPath.Length == 0 || string.Equals( fixedPath, mod.Meta.Name, StringComparison.InvariantCultureIgnoreCase ) )
            {
                Config.ModSortOrder.Remove( mod.BasePath.Name );
                return true;
            }

            if( path != fixedPath )
            {
                Config.ModSortOrder[ mod.BasePath.Name ] = fixedPath;
                return true;
            }

            return false;
        }

        private void SetModStructure( bool removeOldPaths = false )
        {
            var changes = false;

            foreach( var (folder, path) in Config.ModSortOrder.ToArray() )
            {
                if( path.Length > 0 && _mods.FindFirst( m => m.BasePath.Name == folder, out var mod ) )
                {
                    changes |= SetSortOrderPath( mod, path );
                }
                else if( removeOldPaths )
                {
                    changes = true;
                    Config.ModSortOrder.Remove( folder );
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

                ModChange?.Invoke( ChangeType.Removed, idx, mod );
            }
        }

        public int AddMod( DirectoryInfo modFolder )
        {
            var mod = LoadMod( StructuredMods, modFolder );
            if( mod == null )
            {
                return -1;
            }

            if( Config.ModSortOrder.TryGetValue( mod.BasePath.Name, out var sortOrder ) )
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
            ModChange?.Invoke( ChangeType.Added, _mods.Count - 1, mod );

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
                if( Config.ModSortOrder.TryGetValue( mod.BasePath.Name, out var sortOrder ) )
                {
                    mod.Move( sortOrder );
                    var path = mod.Order.FullPath;
                    if( path != sortOrder )
                    {
                        Config.ModSortOrder[ mod.BasePath.Name ] = path;
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
            ModChange?.Invoke( ChangeType.Changed, idx, mod );
            return true;
        }

        public bool UpdateMod( Mod mod, bool reloadMeta = false, bool recomputeMeta = false, bool force = false )
            => UpdateMod( Mods.IndexOf( mod ), reloadMeta, recomputeMeta, force );

        public static FullPath? ResolvePath( Utf8GamePath gameResourcePath )
            => Penumbra.CollectionManager.Default.ResolvePath( gameResourcePath );
    }
}