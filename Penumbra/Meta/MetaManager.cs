using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Lumina.Data.Files;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Util;
using Penumbra.Meta.Files;
using Penumbra.Util;

namespace Penumbra.Meta;

public class MetaManager : IDisposable
{
    internal class FileInformation
    {
        public readonly object    Data;
        public          bool      Changed;
        public          FullPath? CurrentFile;
        public          byte[]    ByteData = Array.Empty< byte >();

        public FileInformation( object data )
            => Data = data;

        public void Write( DirectoryInfo dir, Utf8GamePath originalPath )
        {
            ByteData = Data switch
            {
                EqdpFile eqdp => eqdp.WriteBytes(),
                EqpFile eqp   => eqp.WriteBytes(),
                GmpFile gmp   => gmp.WriteBytes(),
                EstFile est   => est.WriteBytes(),
                ImcFile imc   => imc.WriteBytes(),
                CmpFile cmp   => cmp.WriteBytes(),
                _             => throw new NotImplementedException(),
            };
            DisposeFile( CurrentFile );
            CurrentFile = new FullPath( TempFile.WriteNew( dir, ByteData, $"_{originalPath.Filename()}" ) );
            Changed     = false;
        }
    }

    public const string TmpDirectory = "penumbrametatmp";

    private readonly DirectoryInfo                        _dir;
    private readonly Dictionary< Utf8GamePath, FullPath > _resolvedFiles;

    private readonly Dictionary< MetaManipulation, Mod.Mod >     _currentManipulations = new();
    private readonly Dictionary< Utf8GamePath, FileInformation > _currentFiles         = new();

    public IEnumerable< (MetaManipulation, Mod.Mod) > Manipulations
        => _currentManipulations.Select( kvp => ( kvp.Key, kvp.Value ) );

    public IEnumerable< (Utf8GamePath, FullPath) > Files
        => _currentFiles.Where( kvp => kvp.Value.CurrentFile != null )
           .Select( kvp => ( kvp.Key, kvp.Value.CurrentFile!.Value ) );

    public int Count
        => _currentManipulations.Count;

    public bool TryGetValue( MetaManipulation manip, out Mod.Mod mod )
        => _currentManipulations.TryGetValue( manip, out mod! );

    public byte[] EqpData = Array.Empty< byte >();

    private static void DisposeFile( FullPath? file )
    {
        if( !( file?.Exists ?? false ) )
        {
            return;
        }

        try
        {
            File.Delete( file.Value.FullName );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not delete temporary file \"{file.Value.FullName}\":\n{e}" );
        }
    }

    public void Reset( bool reload = true )
    {
        foreach( var file in _currentFiles )
        {
            _resolvedFiles.Remove( file.Key );
            DisposeFile( file.Value.CurrentFile );
        }

        _currentManipulations.Clear();
        _currentFiles.Clear();
        ClearDirectory();
        if( reload )
        {
            Penumbra.ResidentResources.Reload();
        }
    }

    public void Dispose()
        => Reset();

    private static void ClearDirectory( DirectoryInfo modDir )
    {
        modDir.Refresh();
        if( modDir.Exists )
        {
            try
            {
                Directory.Delete( modDir.FullName, true );
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Could not clear temporary metafile directory \"{modDir.FullName}\":\n{e}" );
            }
        }
    }

    private void ClearDirectory()
        => ClearDirectory( _dir );

    public MetaManager( string name, Dictionary< Utf8GamePath, FullPath > resolvedFiles, DirectoryInfo tempDir )
    {
        _resolvedFiles = resolvedFiles;
        _dir           = new DirectoryInfo( Path.Combine( tempDir.FullName, name.ReplaceBadXivSymbols() ) );
        ClearDirectory();
    }

    public void WriteNewFiles()
    {
        if( _currentFiles.Any() )
        {
            Directory.CreateDirectory( _dir.FullName );
        }

        foreach( var (key, value) in _currentFiles.Where( kvp => kvp.Value.Changed ) )
        {
            value.Write( _dir, key );
            _resolvedFiles[ key ] = value.CurrentFile!.Value;
            if( value.Data is EqpFile )
            {
                EqpData = value.ByteData;
            }
        }
    }

    public bool ApplyMod( MetaManipulation m, Mod.Mod mod )
    {
        if( _currentManipulations.ContainsKey( m ) )
        {
            return false;
        }

        _currentManipulations.Add( m, mod );
        var gamePath = Utf8GamePath.FromString(m.CorrespondingFilename(), out var p, false) ? p : Utf8GamePath.Empty; // TODO
        try
        {
            if( !_currentFiles.TryGetValue( gamePath, out var file ) )
            {
                file = new FileInformation( Penumbra.MetaDefaults.CreateNewFile( m ) ?? throw new IOException() )
                {
                    Changed     = true,
                    CurrentFile = null,
                };
                _currentFiles[ gamePath ] = file;
            }

            file.Changed |= m.Type switch
            {
                MetaType.Eqp  => m.Apply( ( EqpFile )file.Data ),
                MetaType.Eqdp => m.Apply( ( EqdpFile )file.Data ),
                MetaType.Gmp  => m.Apply( ( GmpFile )file.Data ),
                MetaType.Est  => m.Apply( ( EstFile )file.Data ),
                MetaType.Imc  => m.Apply( ( ImcFile )file.Data ),
                MetaType.Rsp  => m.Apply( ( CmpFile )file.Data ),
                _             => throw new NotImplementedException(),
            };
            return true;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not obtain default file for manipulation {m.CorrespondingFilename()}:\n{e}" );
            return false;
        }
    }
}