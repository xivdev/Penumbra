using System;
using System.IO;
using OtterGui.Filesystem;

namespace Penumbra.Mods;

public sealed class ModFileSystemA : FileSystem< Mod >, IDisposable
{
    // Save the current sort order.
    // Does not save or copy the backup in the current mod directory,
    // as this is done on mod directory changes only.
    public void Save()
        => SaveToFile( new FileInfo( Mod.Manager.SortOrderFile ), SaveMod, true );

    // Create a new ModFileSystem from the currently loaded mods and the current sort order file.
    public static ModFileSystemA Load()
    {
        var ret = new ModFileSystemA();
        ret.Reload();

        ret.Changed                              += ret.OnChange;
        Penumbra.ModManager.ModDiscoveryFinished += ret.Reload;

        return ret;
    }

    public void Dispose()
        => Penumbra.ModManager.ModDiscoveryFinished -= Reload;

    // Reload the whole filesystem from currently loaded mods and the current sort order file.
    // Used on construction and on mod rediscoveries.
    private void Reload()
    {
        if( Load( new FileInfo( Mod.Manager.SortOrderFile ), Penumbra.ModManager.Mods, ModToIdentifier, ModToName ) )
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

    // Used for saving and loading.
    private static string ModToIdentifier( Mod mod )
        => mod.BasePath.Name;

    private static string ModToName( Mod mod )
        => mod.Meta.Name.Text;

    private static (string, bool) SaveMod( Mod mod, string fullPath )
    {
        // Only save pairs with non-default paths.
        if( fullPath == ModToName( mod ) )
        {
            return ( string.Empty, false );
        }

        return ( ModToIdentifier( mod ), true );
    }
}