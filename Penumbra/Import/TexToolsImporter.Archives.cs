using Dalamud.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui.Filesystem;
using Penumbra.Import.Structs;
using Penumbra.Mods;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.IO;
using System.Linq;

namespace Penumbra.Import;

public partial class TexToolsImporter
{
    // Extract regular compressed archives that are folders containing penumbra-formatted mods.
    // The mod has to either contain a meta.json at top level, or one folder deep.
    // If the meta.json is one folder deep, all other files have to be in the same folder.
    // The extracted folder gets its name either from that one top-level folder or from the mod name.
    // All data is extracted without manipulation of the files or metadata.
    private DirectoryInfo HandleRegularArchive( FileInfo modPackFile )
    {
        using var zfs     = modPackFile.OpenRead();
        using var archive = ArchiveFactory.Open( zfs );

        var baseName = FindArchiveModMeta( archive, out var leadDir );
        var name     = string.Empty;
        _currentOptionIdx  = 0;
        _currentNumOptions = 1;
        _currentModName    = modPackFile.Name;
        _currentGroupName  = string.Empty;
        _currentOptionName = DefaultTexToolsData.Name;
        _currentNumFiles =
            archive switch
            {
                RarArchive r      => r.Entries.Count,
                ZipArchive z      => z.Entries.Count,
                SevenZipArchive s => s.Entries.Count,
                _                 => archive.Entries.Count(),
            };
        Penumbra.Log.Information( $"    -> Importing {archive.Type} Archive." );

        _currentModDirectory = Mod.Creator.CreateModFolder( _baseDirectory, Path.GetRandomFileName() );
        var options = new ExtractionOptions()
        {
            ExtractFullPath = true,
            Overwrite       = true,
        };

        State           = ImporterState.ExtractingModFiles;
        _currentFileIdx = 0;
        var reader = archive.ExtractAllEntries();

        while( reader.MoveToNextEntry() )
        {
            _token.ThrowIfCancellationRequested();

            if( reader.Entry.IsDirectory )
            {
                --_currentNumFiles;
                continue;
            }

            Penumbra.Log.Information( $"        -> Extracting {reader.Entry.Key}" );
            // Check that the mod has a valid name in the meta.json file.
            if( Path.GetFileName( reader.Entry.Key ) == "meta.json" )
            {
                using var s = new MemoryStream();
                using var e = reader.OpenEntryStream();
                e.CopyTo( s );
                s.Seek( 0, SeekOrigin.Begin );
                using var t   = new StreamReader( s );
                using var j   = new JsonTextReader( t );
                var       obj = JObject.Load( j );
                name = obj[ nameof( Mod.Name ) ]?.Value< string >()?.RemoveInvalidPathSymbols() ?? string.Empty;
                if( name.Length == 0 )
                {
                    throw new Exception( "Invalid mod archive: mod meta has no name." );
                }

                using var f = File.OpenWrite( Path.Combine( _currentModDirectory.FullName, reader.Entry.Key ) );
                s.Seek( 0, SeekOrigin.Begin );
                s.WriteTo( f );
            }
            else
            {
                reader.WriteEntryToDirectory( _currentModDirectory.FullName, options );
            }

            ++_currentFileIdx;
        }

        _token.ThrowIfCancellationRequested();
        var oldName = _currentModDirectory.FullName;
        // Use either the top-level directory as the mods base name, or the (fixed for path) name in the json.
        if( leadDir )
        {
            _currentModDirectory = Mod.Creator.CreateModFolder( _baseDirectory, baseName, false );
            Directory.Move( Path.Combine( oldName, baseName ), _currentModDirectory.FullName );
            Directory.Delete( oldName );
        }
        else
        {
            _currentModDirectory = Mod.Creator.CreateModFolder( _baseDirectory, name, false );
            Directory.Move( oldName, _currentModDirectory.FullName );
        }

        _currentModDirectory.Refresh();
        Mod.Creator.SplitMultiGroups( _currentModDirectory );

        return _currentModDirectory;
    }


    // Search the archive for the meta.json file which needs to exist.
    private static string FindArchiveModMeta( IArchive archive, out bool leadDir )
    {
        var entry = archive.Entries.FirstOrDefault( e => !e.IsDirectory && Path.GetFileName( e.Key ) == "meta.json" );
        // None found.
        if( entry == null )
        {
            throw new Exception( "Invalid mod archive: No meta.json contained." );
        }

        var ret = string.Empty;
        leadDir = false;

        // If the file is not at top-level.
        if( entry.Key != "meta.json" )
        {
            leadDir = true;
            var directory = Path.GetDirectoryName( entry.Key );
            // Should not happen.
            if( directory.IsNullOrEmpty() )
            {
                throw new Exception( "Invalid mod archive: Unknown error fetching meta.json." );
            }

            ret = directory;
            // Check that all other files are also contained in the top-level directory.
            if( ret.IndexOfAny( new[] { '/', '\\' } ) >= 0
            || !archive.Entries.All( e => e.Key.StartsWith( ret ) && ( e.Key.Length == ret.Length || e.Key[ ret.Length ] is '/' or '\\' ) ) )
            {
                throw new Exception(
                    "Invalid mod archive: meta.json in wrong location. It needs to be either at root or one directory deep, in which all other files must be nested too." );
            }
        }


        return ret;
    }
}