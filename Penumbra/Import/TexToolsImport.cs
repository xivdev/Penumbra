using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Logging;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using Penumbra.Mods;
using FileMode = System.IO.FileMode;

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

    public TexToolsImporter( DirectoryInfo baseDirectory, ICollection< FileInfo > files,
        Action< FileInfo, DirectoryInfo?, Exception? > handler )
        : this( baseDirectory, files.Count, files, handler )
    { }

    public TexToolsImporter( DirectoryInfo baseDirectory, int count, IEnumerable< FileInfo > modPackFiles,
        Action< FileInfo, DirectoryInfo?, Exception? > handler )
    {
        _baseDirectory = baseDirectory;
        _tmpFile       = Path.Combine( _baseDirectory.FullName, TempFileName );
        _modPackFiles  = modPackFiles;
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
        State              = ImporterState.None;
        _currentModPackIdx = 0;
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
                if( Penumbra.Config.AutoDeduplicateOnImport )
                {
                    State = ImporterState.DeduplicatingFiles;
                    Mod.Editor.DeduplicateMod( directory );
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
        using var zfs              = modPackFile.OpenRead();
        using var extractedModPack = new ZipFile( zfs );

        var mpl = FindZipEntry( extractedModPack, "TTMPL.mpl" );
        if( mpl == null )
        {
            throw new FileNotFoundException( "ZIP does not contain a TTMPL.mpl file." );
        }

        var modRaw = GetStringFromZipEntry( extractedModPack, mpl, Encoding.UTF8 );

        // At least a better validation than going by the extension.
        if( modRaw.Contains( "\"TTMPVersion\":" ) )
        {
            if( modPackFile.Extension != ".ttmp2" )
            {
                PluginLog.Warning( $"File {modPackFile.FullName} seems to be a V2 TTMP, but has the wrong extension." );
            }

            return ImportV2ModPack( modPackFile, extractedModPack, modRaw );
        }

        if( modPackFile.Extension != ".ttmp" )
        {
            PluginLog.Warning( $"File {modPackFile.FullName} seems to be a V1 TTMP, but has the wrong extension." );
        }

        return ImportV1ModPack( modPackFile, extractedModPack, modRaw );
    }


    // You can in no way rely on any file paths in TTMPs so we need to just do this, sorry
    private static ZipEntry? FindZipEntry( ZipFile file, string fileName )
    {
        for( var i = 0; i < file.Count; i++ )
        {
            var entry = file[ i ];

            if( entry.Name.Contains( fileName ) )
            {
                return entry;
            }
        }

        return null;
    }

    private static Stream GetStreamFromZipEntry( ZipFile file, ZipEntry entry )
        => file.GetInputStream( entry );

    private static string GetStringFromZipEntry( ZipFile file, ZipEntry entry, Encoding encoding )
    {
        using var ms = new MemoryStream();
        using var s  = GetStreamFromZipEntry( file, entry );
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

    private StreamDisposer GetSqPackStreamStream( ZipFile file, string entryName )
    {
        State = ImporterState.WritingPackToDisk;

        // write shitty zip garbage to disk
        var entry = FindZipEntry( file, entryName );
        if( entry == null )
        {
            throw new FileNotFoundException( $"ZIP does not contain a file named {entryName}." );
        }

        using var s = file.GetInputStream( entry );

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