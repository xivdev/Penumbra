using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Penumbra.GameData.ByteString;
using Penumbra.Meta;
using Penumbra.Mod;
using Penumbra.Util;

namespace Penumbra.Mods;

public enum ModChangeType
{
    Added,
    Removed,
    Changed,
}

public delegate void ModChangeDelegate( ModChangeType type, int modIndex, ModData modData );

// The ModManager handles the basic mods installed to the mod directory.
// It also contains the CollectionManager that handles all collections.
public class ModManager
{
    public DirectoryInfo BasePath { get; private set; } = null!;

    private List< ModData > ModsInternal { get; init; } = new();

    public IReadOnlyList< ModData > Mods
        => ModsInternal;

    public ModFolder StructuredMods { get; } = ModFileSystem.Root;

    public CollectionManager Collections { get; }

    public event ModChangeDelegate? ModChange;

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
            if( path.Length > 0 && ModsInternal.FindFirst( m => m.BasePath.Name == folder, out var mod ) )
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
        ModsInternal.Clear();
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

                ModsInternal.Add( mod );
            }

            SetModStructure();
        }

        Collections.RecreateCaches();
        Collections.DefaultCollection.SetFiles();
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

        var idx = ModsInternal.FindIndex( m => m.BasePath.Name == modFolder.Name );
        if( idx >= 0 )
        {
            var mod = ModsInternal[ idx ];
            mod.SortOrder.ParentFolder.RemoveMod( mod );
            ModsInternal.RemoveAt( idx );
            Collections.RemoveModFromCaches( modFolder );
            ModChange?.Invoke( ModChangeType.Removed, idx, mod );
        }
    }

    public int AddMod( DirectoryInfo modFolder )
    {
        var mod = ModData.LoadMod( StructuredMods, modFolder );
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

        if( ModsInternal.Any( m => m.BasePath.Name == modFolder.Name ) )
        {
            return -1;
        }

        ModsInternal.Add( mod );
        ModChange?.Invoke( ModChangeType.Added, ModsInternal.Count - 1, mod );
        foreach( var collection in Collections.Collections.Values )
        {
            collection.AddMod( mod );
        }

        return ModsInternal.Count - 1;
    }

    public bool UpdateMod( int idx, bool reloadMeta = false, bool recomputeMeta = false, bool force = false )
    {
        var mod         = Mods[ idx ];
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
        ModChange?.Invoke( ModChangeType.Changed, idx, mod );
        return true;
    }

    public bool UpdateMod( ModData mod, bool reloadMeta = false, bool recomputeMeta = false, bool force = false )
        => UpdateMod( Mods.IndexOf( mod ), reloadMeta, recomputeMeta, force );

    public FullPath? ResolveSwappedOrReplacementPath( Utf8GamePath gameResourcePath )
    {
        var ret = Collections.DefaultCollection.ResolveSwappedOrReplacementPath( gameResourcePath );
        ret ??= Collections.ForcedCollection.ResolveSwappedOrReplacementPath( gameResourcePath );
        return ret;
    }
}