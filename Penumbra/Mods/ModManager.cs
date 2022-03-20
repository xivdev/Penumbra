using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Penumbra.GameData.ByteString;
using Penumbra.Meta;
using Penumbra.Mod;

namespace Penumbra.Mods;

// The ModManager handles the basic mods installed to the mod directory.
// It also contains the CollectionManager that handles all collections.
public class ModManager
{
    public DirectoryInfo BasePath { get; private set; } = null!;

    public Dictionary< string, ModData > Mods { get; } = new();
    public ModFolder StructuredMods { get; } = ModFileSystem.Root;

    public CollectionManager Collections { get; }

    public bool Valid { get; private set; }

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

        if( !newPath.Any() )
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

            if( !firstTime )
            {
                Collections.RecreateCaches();
            }
        }
    }

    public ModManager()
    {
        SetBaseDirectory( Config.ModDirectory, true );
        Collections = new CollectionManager( this );
    }

    private bool SetSortOrderPath( ModData mod, string path )
    {
        mod.Move( path );
        var fixedPath = mod.SortOrder.FullPath;
        if( !fixedPath.Any() || string.Equals( fixedPath, mod.Meta.Name, StringComparison.InvariantCultureIgnoreCase ) )
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

        foreach( var kvp in Config.ModSortOrder.ToArray() )
        {
            if( kvp.Value.Any() && Mods.TryGetValue( kvp.Key, out var mod ) )
            {
                changes |= SetSortOrderPath( mod, kvp.Value );
            }
            else if( removeOldPaths )
            {
                changes = true;
                Config.ModSortOrder.Remove( kvp.Key );
            }
        }

        if( changes )
        {
            Config.Save();
        }
    }

    public void DiscoverMods()
    {
        Mods.Clear();
        BasePath.Refresh();

        StructuredMods.SubFolders.Clear();
        StructuredMods.Mods.Clear();
        if( Valid && BasePath.Exists )
        {
            foreach( var modFolder in BasePath.EnumerateDirectories() )
            {
                var mod = ModData.LoadMod( StructuredMods, modFolder );
                if( mod == null )
                {
                    continue;
                }

                Mods.Add( modFolder.Name, mod );
            }

            SetModStructure();
        }

        Collections.RecreateCaches();
        Collections.DefaultCollection.SetFiles();
    }

    public void DeleteMod( DirectoryInfo modFolder )
    {
        modFolder.Refresh();
        if( modFolder.Exists )
        {
            try
            {
                Directory.Delete( modFolder.FullName, true );
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Could not delete the mod {modFolder.Name}:\n{e}" );
            }

            if( Mods.TryGetValue( modFolder.Name, out var mod ) )
            {
                mod.SortOrder.ParentFolder.RemoveMod( mod );
                Mods.Remove( modFolder.Name );
                Collections.RemoveModFromCaches( modFolder );
            }
        }
    }

    public bool AddMod( DirectoryInfo modFolder )
    {
        var mod = ModData.LoadMod( StructuredMods, modFolder );
        if( mod == null )
        {
            return false;
        }

        if( Config.ModSortOrder.TryGetValue( mod.BasePath.Name, out var sortOrder ) )
        {
            if( SetSortOrderPath( mod, sortOrder ) )
            {
                Config.Save();
            }
        }

        if( Mods.ContainsKey( modFolder.Name ) )
        {
            return false;
        }

        Mods.Add( modFolder.Name, mod );
        foreach( var collection in Collections.Collections.Values )
        {
            collection.AddMod( mod );
        }

        return true;
    }

    public bool UpdateMod( ModData mod, bool reloadMeta = false, bool recomputeMeta = false, bool force = false )
    {
        var oldName     = mod.Meta.Name;
        var metaChanges = mod.Meta.RefreshFromFile( mod.MetaFile ) || force;
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
                var path = mod.SortOrder.FullPath;
                if( path != sortOrder )
                {
                    Config.ModSortOrder[ mod.BasePath.Name ] = path;
                    Config.Save();
                }
            }
            else
            {
                mod.SortOrder = new SortOrder( StructuredMods, mod.Meta.Name );
            }
        }

        var nameChange = !string.Equals( oldName, mod.Meta.Name, StringComparison.InvariantCulture );

        recomputeMeta |= fileChanges.HasFlag( ResourceChange.Meta );
        if( recomputeMeta )
        {
            mod.Resources.MetaManipulations.Update( mod.Resources.MetaFiles, mod.BasePath, mod.Meta );
            mod.Resources.MetaManipulations.SaveToFile( MetaCollection.FileName( mod.BasePath ) );
        }

        Collections.UpdateCollections( mod, metaChanges, fileChanges, nameChange, reloadMeta );

        return true;
    }

    public FullPath? ResolveSwappedOrReplacementPath( Utf8GamePath gameResourcePath )
    {
        var ret = Collections.DefaultCollection.ResolveSwappedOrReplacementPath( gameResourcePath );
        ret ??= Collections.ForcedCollection.ResolveSwappedOrReplacementPath( gameResourcePath );
        return ret;
    }

    //         private void FileSystemWatcherOnChanged( object sender, FileSystemEventArgs e )
    //         {
    // #if DEBUG
    //             PluginLog.Verbose( "file changed: {FullPath}", e.FullPath );
    // #endif
    //
    //             if( _plugin.ImportInProgress )
    //             {
    //                 return;
    //             }
    //
    //             if( _plugin.Configuration.DisableFileSystemNotifications )
    //             {
    //                 return;
    //             }
    //
    //             var file = e.FullPath;
    //
    //             if( !ResolvedFiles.Any( x => x.Value.FullName == file ) )
    //             {
    //                 return;
    //             }
    //
    //             PluginLog.Log( "a loaded file has been modified - file: {FullPath}", file );
    //             _plugin.GameUtils.ReloadPlayerResources();
    //         }
    // 
    //         private void FileSystemPasta()
    //         {
    //              haha spaghet
    //              _fileSystemWatcher?.Dispose();
    //              _fileSystemWatcher = new FileSystemWatcher( _basePath.FullName )
    //              {
    //                  NotifyFilter = NotifyFilters.LastWrite |
    //                                 NotifyFilters.FileName |
    //                                 NotifyFilters.DirectoryName,
    //                  IncludeSubdirectories = true,
    //                  EnableRaisingEvents = true
    //              };
    //             
    //              _fileSystemWatcher.Changed += FileSystemWatcherOnChanged;
    //              _fileSystemWatcher.Created += FileSystemWatcherOnChanged;
    //              _fileSystemWatcher.Deleted += FileSystemWatcherOnChanged;
    //              _fileSystemWatcher.Renamed += FileSystemWatcherOnChanged;
    //         }
}