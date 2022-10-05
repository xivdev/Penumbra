using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Penumbra.Mods;

// Utility to create and apply a zipped backup of a mod.
public class ModBackup
{
    public static bool CreatingBackup { get; private set; }

    private readonly Mod    _mod;
    public readonly  string Name;
    public readonly  bool   Exists;

    public ModBackup( Mod mod )
    {
        _mod   = mod;
        Name   = Path.Combine( Penumbra.ModManager.ExportDirectory.FullName, _mod.ModPath.Name ) + ".pmp";
        Exists = File.Exists( Name );
    }

    // Migrate file extensions.
    public static void MigrateZipToPmp( Mod.Manager manager )
    {
        foreach( var mod in manager )
        {
            var pmpName = mod.ModPath + ".pmp";
            var zipName = mod.ModPath + ".zip";
            if( File.Exists( zipName ) )
            {
                try
                {
                    if( !File.Exists( pmpName ) )
                    {
                        File.Move( zipName, pmpName );
                    }
                    else
                    {
                        File.Delete( zipName );
                    }

                    Penumbra.Log.Information( $"Migrated mod export from {zipName} to {pmpName}." );
                }
                catch( Exception e )
                {
                    Penumbra.Log.Warning( $"Could not migrate mod export of {mod.ModPath} from .pmp to .zip:\n{e}" );
                }
            }
        }
    }

    // Move and/or rename an exported mod.
    // This object is unusable afterwards.
    public void Move( string? newBasePath = null, string? newName = null )
    {
        if( CreatingBackup || !Exists )
        {
            return;
        }

        try
        {
            newBasePath ??= Path.GetDirectoryName( Name ) ?? string.Empty;
            newName     =   newName == null ? Path.GetFileName( Name ) : newName + ".pmp";
            var newPath = Path.Combine( newBasePath, newName );
            File.Move( Name, newPath );
        }
        catch( Exception e )
        {
            Penumbra.Log.Warning( $"Could not move mod export file {Name}:\n{e}" );
        }
    }

    // Create a backup zip without blocking the main thread.
    public async void CreateAsync()
    {
        if( CreatingBackup )
        {
            return;
        }

        CreatingBackup = true;
        await Task.Run( Create );
        CreatingBackup = false;
    }


    // Create a backup. Overwrites pre-existing backups.
    private void Create()
    {
        try
        {
            Delete();
            ZipFile.CreateFromDirectory( _mod.ModPath.FullName, Name, CompressionLevel.Optimal, false );
            Penumbra.Log.Debug( $"Created export file {Name} from {_mod.ModPath.FullName}." );
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Could not export mod {_mod.Name} to \"{Name}\":\n{e}" );
        }
    }

    // Delete a pre-existing backup.
    public void Delete()
    {
        if( !Exists )
        {
            return;
        }

        try
        {
            File.Delete( Name );
            Penumbra.Log.Debug( $"Deleted export file {Name}." );
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Could not delete file \"{Name}\":\n{e}" );
        }
    }

    // Restore a mod from a pre-existing backup. Does not check if the mod contained in the backup is even similar.
    // Does an automatic reload after extraction.
    public void Restore()
    {
        try
        {
            if( Directory.Exists( _mod.ModPath.FullName ) )
            {
                Directory.Delete( _mod.ModPath.FullName, true );
                Penumbra.Log.Debug( $"Deleted mod folder {_mod.ModPath.FullName}." );
            }

            ZipFile.ExtractToDirectory( Name, _mod.ModPath.FullName );
            Penumbra.Log.Debug( $"Extracted exported file {Name} to {_mod.ModPath.FullName}." );
            Penumbra.ModManager.ReloadMod( _mod.Index );
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Could not restore {_mod.Name} from export \"{Name}\":\n{e}" );
        }
    }
}