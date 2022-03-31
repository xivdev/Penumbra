using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Dalamud.Logging;

namespace Penumbra.Util;

public static class Backup
{
    public const int MaxNumBackups = 10;

    // Create a backup named by ISO 8601 of the current time.
    // If the newest previously existing backup equals the current state of files,
    // do not create a new backup.
    // If the maximum number of backups is exceeded afterwards, delete the oldest backup.
    public static void CreateBackup( IReadOnlyCollection< FileInfo > files )
    {
        try
        {
            var configDirectory = Dalamud.PluginInterface.ConfigDirectory.Parent!.FullName;
            var directory       = CreateBackupDirectory();
            var (newestFile, oldestFile, numFiles) = CheckExistingBackups( directory );
            var newBackupName = Path.Combine( directory.FullName, $"{DateTime.Now:yyyyMMddHHmss}.zip" );
            if( newestFile == null || CheckNewestBackup( newestFile, configDirectory, files.Count ) )
            {
                CreateBackup( files, newBackupName, configDirectory );
                if( numFiles > MaxNumBackups )
                {
                    oldestFile!.Delete();
                }
            }
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not create backups:\n{e}" );
        }
    }


    // Obtain the backup directory. Create it if it does not exist.
    private static DirectoryInfo CreateBackupDirectory()
    {
        var path = Path.Combine( Dalamud.PluginInterface.ConfigDirectory.Parent!.Parent!.FullName, "backups",
            Dalamud.PluginInterface.ConfigDirectory.Name );
        var dir = new DirectoryInfo( path );
        if( !dir.Exists )
        {
            dir = Directory.CreateDirectory( dir.FullName );
        }

        return dir;
    }

    // Check the already existing backups.
    // Only keep MaxNumBackups at once, and delete the oldest if the number would be exceeded.
    // Return the newest backup.
    private static (FileInfo? Newest, FileInfo? Oldest, int Count) CheckExistingBackups( DirectoryInfo backupDirectory )
    {
        var       count  = 0;
        FileInfo? newest = null;
        FileInfo? oldest = null;

        foreach( var file in backupDirectory.EnumerateFiles( "*.zip" ) )
        {
            ++count;
            var time = file.CreationTimeUtc;
            if( ( oldest?.CreationTimeUtc ?? DateTime.MinValue ) < time )
            {
                oldest = file;
            }

            if( ( newest?.CreationTimeUtc ?? DateTime.MaxValue ) > time )
            {
                newest = file;
            }
        }

        return ( newest, oldest, count );
    }

    // Compare the newest backup against the currently existing files.
    // If there are any differences, return false, and if they are completely identical, return true.
    private static bool CheckNewestBackup( FileInfo newestFile, string configDirectory, int fileCount )
    {
        using var oldFileStream = File.Open( newestFile.FullName, FileMode.Open );
        using var oldZip        = new ZipArchive( oldFileStream, ZipArchiveMode.Read );
        // Number of stored files is different.
        if( fileCount != oldZip.Entries.Count )
        {
            return true;
        }

        // Since number of files is identical,
        // the backups are identical if every file in the old backup
        // still exists and is identical.
        foreach( var entry in oldZip.Entries )
        {
            var file = Path.Combine( configDirectory, entry.FullName );
            if( !File.Exists( file ) )
            {
                return true;
            }

            using var currentData = File.OpenRead( file );
            using var oldData     = entry.Open();

            if( !Equals( currentData, oldData ) )
            {
                return true;
            }
        }

        return false;
    }

    // Create the actual backup, storing all the files relative to the given configDirectory in the zip.
    private static void CreateBackup( IEnumerable< FileInfo > files, string fileName, string configDirectory )
    {
        using var fileStream = File.Open( fileName, FileMode.Create );
        using var zip        = new ZipArchive( fileStream, ZipArchiveMode.Create );
        foreach( var file in files )
        {
            zip.CreateEntryFromFile( file.FullName, Path.GetRelativePath( configDirectory, file.FullName ), CompressionLevel.Optimal );
        }
    }

    // Compare two streams per byte and return if they are equal.
    private static bool Equals( Stream lhs, Stream rhs )
    {
        while( true )
        {
            var current = lhs.ReadByte();
            var old     = rhs.ReadByte();
            if( current != old )
            {
                return false;
            }

            if( current == -1 )
            {
                return true;
            }
        }
    }
}