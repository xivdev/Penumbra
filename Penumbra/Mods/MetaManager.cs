using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Plugin;
using Lumina.Data.Files;
using Penumbra.Game;
using Penumbra.Hooks;
using Penumbra.Util;
using Penumbra.MetaData;

namespace Penumbra.Mods
{
    public class MetaManager : IDisposable
    {
        private class FileInformation
        {
            public readonly object    Data;
            public          bool      Changed;
            public          FileInfo? CurrentFile;

            public FileInformation( object data )
                => Data = data;

            public void Write( DirectoryInfo dir )
            {
                byte[] data = Data switch
                {
                    EqdpFile eqdp => eqdp.WriteBytes(),
                    EqpFile eqp   => eqp.WriteBytes(),
                    GmpFile gmp   => gmp.WriteBytes(),
                    EstFile est   => est.WriteBytes(),
                    ImcFile imc   => imc.WriteBytes(),
                    _             => throw new NotImplementedException()
                };
                DisposeFile( CurrentFile );
                CurrentFile = TempFile.WriteNew( dir, data );
                Changed     = false;
            }
        }

        private const string TmpDirectory = "penumbrametatmp";

        private readonly MetaDefaults                     _default;
        private readonly DirectoryInfo                    _dir;
        private readonly GameResourceManagement           _resourceManagement;
        private readonly Dictionary< GamePath, FileInfo > _resolvedFiles;

        private readonly HashSet< MetaManipulation >             _currentManipulations = new();
        private readonly Dictionary< GamePath, FileInformation > _currentFiles         = new();

        private static void DisposeFile( FileInfo? file )
        {
            if( !( file?.Exists ?? false ) )
            {
                return;
            }

            try
            {
                file.Delete();
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Could not delete temporary file \"{file.FullName}\":\n{e}" );
            }
        }

        public void Dispose()
        {
            foreach( var file in _currentFiles )
            {
                _resolvedFiles.Remove( file.Key );
                DisposeFile( file.Value.CurrentFile );
            }

            _currentManipulations.Clear();
            _currentFiles.Clear();
            ClearDirectory();
        }

        private void ClearDirectory()
        {
            if( _dir.Exists )
            {
                try
                {
                    Directory.Delete( _dir.FullName, true );
                }
                catch( Exception e )
                {
                    PluginLog.Error( $"Could not clear temporary metafile directory \"{_dir.FullName}\":\n{e}" );
                }
            }
        }

        public MetaManager( Dictionary< GamePath, FileInfo > resolvedFiles, DirectoryInfo modDir )
        {
            _resolvedFiles      = resolvedFiles;
            _default            = Service< MetaDefaults >.Get();
            _resourceManagement = Service< GameResourceManagement >.Get();
            _dir                = new DirectoryInfo( Path.Combine( modDir.FullName, TmpDirectory ) );
            ClearDirectory();
            Directory.CreateDirectory( _dir.FullName );
        }

        public void WriteNewFiles()
        {
            foreach( var kvp in _currentFiles.Where( kvp => kvp.Value.Changed ) )
            {
                kvp.Value.Write( _dir );
                _resolvedFiles[ kvp.Key ] = kvp.Value.CurrentFile!;
            }

            _resourceManagement.ReloadPlayerResources();
        }

        public bool ApplyMod( MetaManipulation m )
        {
            if( !_currentManipulations.Add( m ) )
            {
                return false;
            }

            var gamePath = m.CorrespondingFilename();
            if( !_currentFiles.TryGetValue( gamePath, out var file ) )
            {
                file = new FileInformation( _default.CreateNewFile( m ) ?? throw new IOException() )
                {
                    Changed     = true,
                    CurrentFile = null
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
                _             => throw new NotImplementedException()
            };

            return true;
        }
    }
}