using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Penumbra.Api;
using Penumbra.Import.Structs;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using FileMode = System.IO.FileMode;
using ZipArchive = SharpCompress.Archives.Zip.ZipArchive;
using ZipArchiveEntry = SharpCompress.Archives.Zip.ZipArchiveEntry;

namespace Penumbra.Import;

public partial class TexToolsImporter : IDisposable
{
    private const           string                 TempFileName = "textools-import";
    private static readonly JsonSerializerSettings JsonSettings = new() { NullValueHandling = NullValueHandling.Ignore };

    private readonly DirectoryInfo _baseDirectory;
    private readonly string        _tmpFile;

    private readonly IEnumerable< FileInfo > _modPackFiles;
    private readonly int                     _modPackCount;
    private          FileStream?             _tmpFileStream;
    private          StreamDisposer?         _streamDisposer;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly CancellationToken       _token;

    public ImporterState State { get; private set; }
    public readonly List< (FileInfo File, DirectoryInfo? Mod, Exception? Error) > ExtractedMods;

    private readonly Configuration _config;
    private readonly ModEditor     _editor;
    private readonly ModManager   _modManager;

    public TexToolsImporter( DirectoryInfo baseDirectory, int count, IEnumerable< FileInfo > modPackFiles,
        Action< FileInfo, DirectoryInfo?, Exception? > handler, Configuration config, ModEditor editor, ModManager modManager)
    {
        _baseDirectory = baseDirectory;
        _tmpFile       = Path.Combine( _baseDirectory.FullName, TempFileName );
        _modPackFiles  = modPackFiles;
        _config        = config;
        _editor        = editor;
        _modManager    = modManager; 
        _modPackCount  = count;
        ExtractedMods  = new List< (FileInfo, DirectoryInfo?, Exception?) >( count );
        _token         = _cancellation.Token;
        Task.Run( ImportFiles, _token )
           .ContinueWith( _ => CloseStreams() )
           .ContinueWith( _ =>
            {
                foreach( var (file, dir, error) in ExtractedMods )
                {
                    handler( file, dir, error );
                }
            } );
    }

    private void CloseStreams()
    {
        _tmpFileStream?.Dispose();
        _tmpFileStream = null;
        ResetStreamDisposer();
    }

    public void Dispose()
    {
        _cancellation.Cancel( true );
        if( State != ImporterState.WritingPackToDisk )
        {
            _tmpFileStream?.Dispose();
            _tmpFileStream = null;
        }

        if( State != ImporterState.ExtractingModFiles )
        {
            ResetStreamDisposer();
        }
    }

    private void ImportFiles()
    {
        State                    = ImporterState.None;
        _currentModPackIdx       = 0;
        foreach( var file in _modPackFiles )
        {
            _currentModDirectory = null;
            if( _token.IsCancellationRequested )
            {
                ExtractedMods.Add( ( file, null, new TaskCanceledException( "Task canceled by user." ) ) );
                continue;
            }

            try
            {
                var directory = VerifyVersionAndImport( file );
                ExtractedMods.Add( ( file, directory, null ) );
                if( _config.AutoDeduplicateOnImport )
                {
                    State = ImporterState.DeduplicatingFiles;
                    _editor.Duplicates.DeduplicateMod( directory );
                }
            }
            catch( Exception e )
            {
                ExtractedMods.Add( ( file, _currentModDirectory, e ) );
                _currentNumOptions = 0;
                _currentOptionIdx  = 0;
                _currentFileIdx    = 0;
                _currentNumFiles   = 0;
            }

            ++_currentModPackIdx;
        }

        State = ImporterState.Done;
    }

    // Rudimentary analysis of a TTMP file by extension and version.
    // Puts out warnings if extension does not correspond to data.
    private DirectoryInfo VerifyVersionAndImport( FileInfo modPackFile )
    {
        if( modPackFile.Extension.ToLowerInvariant() is ".pmp" or ".zip" or ".7z" or ".rar" )
        {
            return HandleRegularArchive( modPackFile );
        }

        using var zfs              = modPackFile.OpenRead();
        using var extractedModPack = ZipArchive.Open( zfs );

        var mpl = FindZipEntry( extractedModPack, "TTMPL.mpl" );
        if( mpl == null )
        {
            throw new FileNotFoundException( "ZIP does not contain a TTMPL.mpl file." );
        }

        var modRaw = GetStringFromZipEntry( mpl, Encoding.UTF8 );

        // At least a better validation than going by the extension.
        if( modRaw.Contains( "\"TTMPVersion\":" ) )
        {
            if( modPackFile.Extension != ".ttmp2" )
            {
                Penumbra.Log.Warning( $"File {modPackFile.FullName} seems to be a V2 TTMP, but has the wrong extension." );
            }

            return ImportV2ModPack( modPackFile, extractedModPack, modRaw );
        }

        if( modPackFile.Extension != ".ttmp" )
        {
            Penumbra.Log.Warning( $"File {modPackFile.FullName} seems to be a V1 TTMP, but has the wrong extension." );
        }

        return ImportV1ModPack( modPackFile, extractedModPack, modRaw );
    }

    // You can in no way rely on any file paths in TTMPs so we need to just do this, sorry
    private static ZipArchiveEntry? FindZipEntry( ZipArchive file, string fileName )
        => file.Entries.FirstOrDefault( e => !e.IsDirectory && e.Key.Contains( fileName ) );

    private static string GetStringFromZipEntry( ZipArchiveEntry entry, Encoding encoding )
    {
        using var ms = new MemoryStream();
        using var s  = entry.OpenEntryStream();
        s.CopyTo( ms );
        return encoding.GetString( ms.ToArray() );
    }

    private void WriteZipEntryToTempFile( Stream s )
    {
        _tmpFileStream?.Dispose(); // should not happen
        _tmpFileStream = new FileStream( _tmpFile, FileMode.Create );
        if( _token.IsCancellationRequested )
        {
            return;
        }

        s.CopyTo( _tmpFileStream );
        _tmpFileStream.Dispose();
        _tmpFileStream = null;
    }

    private StreamDisposer GetSqPackStreamStream( ZipArchive file, string entryName )
    {
        State = ImporterState.WritingPackToDisk;

        // write shitty zip garbage to disk
        var entry = FindZipEntry( file, entryName );
        if( entry == null )
        {
            throw new FileNotFoundException( $"ZIP does not contain a file named {entryName}." );
        }

        using var s = entry.OpenEntryStream();

        WriteZipEntryToTempFile( s );

        _streamDisposer?.Dispose(); // Should not happen.
        var fs = new FileStream( _tmpFile, FileMode.Open );
        return new StreamDisposer( fs );
    }

    private void ResetStreamDisposer()
    {
        _streamDisposer?.Dispose();
        _streamDisposer = null;
    }
}