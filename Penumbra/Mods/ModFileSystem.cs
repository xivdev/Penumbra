using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OtterGui.Filesystem;

namespace Penumbra.Mods;

public sealed class ModFileSystem : FileSystem< Mod >, IDisposable
{
    public static string ModFileSystemFile
        => Path.Combine( Dalamud.PluginInterface.GetPluginConfigDirectory(), "sort_order.json" );

    // Save the current sort order.
    // Does not save or copy the backup in the current mod directory,
    // as this is done on mod directory changes only.
    public void Save()
        => SaveToFile( new FileInfo( ModFileSystemFile ), SaveMod, true );

    // Create a new ModFileSystem from the currently loaded mods and the current sort order file.
    public static ModFileSystem Load()
    {
        var ret = new ModFileSystem();
        ret.Reload();

        ret.Changed                              += ret.OnChange;
        Penumbra.ModManager.ModDiscoveryFinished += ret.Reload;
        Penumbra.ModManager.ModMetaChanged       += ret.OnMetaChange;
        Penumbra.ModManager.ModPathChanged       += ret.OnModPathChange;

        return ret;
    }

    public void Dispose()
    {
        Penumbra.ModManager.ModPathChanged       -= OnModPathChange;
        Penumbra.ModManager.ModDiscoveryFinished -= Reload;
        Penumbra.ModManager.ModMetaChanged       -= OnMetaChange;
    }

    // Reload the whole filesystem from currently loaded mods and the current sort order file.
    // Used on construction and on mod rediscoveries.
    private void Reload()
    {
        if( Load( new FileInfo( ModFileSystemFile ), Penumbra.ModManager, ModToIdentifier, ModToName ) )
        {
            Save();
        }
    }

    // Save the filesystem on every filesystem change except full reloading.
    private void OnChange( FileSystemChangeType type, IPath _1, IPath? _2, IPath? _3 )
    {
        if( type != FileSystemChangeType.Reload )
        {
            Save();
        }
    }

    // Update sort order when defaulted mod names change.
    private void OnMetaChange( MetaChangeType type, Mod mod, string? oldName )
    {
        if( type.HasFlag( MetaChangeType.Name ) && oldName != null )
        {
            var old = oldName.FixName();
            if( Find( old, out var child ) )
            {
                Rename( child, mod.Name.Text );
            }
        }
    }

    // Update the filesystem if a mod has been added or removed.
    // Save it, if the mod directory has been moved, since this will change the save format.
    private void OnModPathChange( ModPathChangeType type, Mod mod, DirectoryInfo? oldPath, DirectoryInfo? newPath )
    {
        switch( type )
        {
            case ModPathChangeType.Added:
                var originalName = mod.Name.Text.FixName();
                var name         = originalName;
                var counter      = 1;
                while( Find( name, out _ ) )
                {
                    name = $"{originalName} ({++counter})";
                }

                CreateLeaf( Root, name, mod );
                break;
            case ModPathChangeType.Deleted:
                var leaf = Root.GetAllDescendants( SortMode.Lexicographical ).OfType< Leaf >().FirstOrDefault( l => l.Value == mod );
                if( leaf != null )
                {
                    Delete( leaf );
                }
                break;
            case ModPathChangeType.Moved:
                Save();
                break;
        }
    }

    // Used for saving and loading.
    private static string ModToIdentifier( Mod mod )
        => mod.BasePath.Name;

    private static string ModToName( Mod mod )
        => mod.Name.Text.FixName();

    private static (string, bool) SaveMod( Mod mod, string fullPath )
    {
        var regex = new Regex( $@"^{Regex.Escape( ModToName( mod ) )}( \(\d+\))?" );
        // Only save pairs with non-default paths.
        if( regex.IsMatch( fullPath ) )
        {
            return ( string.Empty, false );
        }

        return ( ModToIdentifier( mod ), true );
    }
}