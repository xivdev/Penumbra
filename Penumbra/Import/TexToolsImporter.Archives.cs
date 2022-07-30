using System;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Dalamud.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui.Filesystem;
using Penumbra.Mods;
using SharpCompress.Archives;
using SharpCompress.Common;

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
        _currentOptionIdx  = 0;
        _currentNumOptions = 1;
        _currentModName    = modPackFile.Name;
        _currentGroupName  = string.Empty;
        _currentOptionName = DefaultTexToolsData.Name;
        _currentNumFiles   = archive.Entries.Count( e => !e.IsDirectory );
        PluginLog.Log( $"    -> Importing {archive.Type} Archive." );

        _currentModDirectory = Mod.CreateModFolder( _baseDirectory, baseName );
        var options = new ExtractionOptions()
        {
            ExtractFullPath = true,
            Overwrite       = true,
        };

        State           = ImporterState.ExtractingModFiles;
        _currentFileIdx = 0;
        foreach( var entry in archive.Entries )
        {
            _token.ThrowIfCancellationRequested();

            if( entry.IsDirectory )
            {
                ++_currentFileIdx;
                continue;
            }

            PluginLog.Log( "        -> Extracting {0}", entry.Key );
            entry.WriteToDirectory( _currentModDirectory.FullName, options );

            ++_currentFileIdx;
        }

        if( leadDir )
        {
            _token.ThrowIfCancellationRequested();
            var oldName = _currentModDirectory.FullName;
            var tmpName = oldName + "__tmp";
            Directory.Move( oldName, tmpName );
            Directory.Move( Path.Combine( tmpName, baseName ), oldName );
            Directory.Delete( tmpName );
            _currentModDirectory = new DirectoryInfo( oldName );
        }

        return _currentModDirectory;
    }

    // Search the archive for the meta.json file which needs to exist.
    private static string FindArchiveModMeta( IArchive archive, out bool leadDir )
    {
        var entry = archive.Entries.FirstOrDefault( e => !e.IsDirectory && e.Key.EndsWith( "meta.json" ) );
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

        // Check that the mod has a valid name in the meta.json file.
        using var e    = entry.OpenEntryStream();
        using var t    = new StreamReader( e );
        using var j    = new JsonTextReader( t );
        var       obj  = JObject.Load( j );
        var       name = obj[ nameof( Mod.Name ) ]?.Value< string >().RemoveInvalidPathSymbols() ?? string.Empty;
        if( name.Length == 0 )
        {
            throw new Exception( "Invalid mod archive: mod meta has no name." );
        }

        // Use either the top-level directory as the mods base name, or the (fixed for path) name in the json.
        return ret.Length == 0 ? name : ret;
    }
}