using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Plugin;
using Lumina.Data.Files;
using Penumbra.Interop;
using Penumbra.Meta.Files;
using Penumbra.Util;

namespace Penumbra.Meta
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
                    _             => throw new NotImplementedException(),
                };
                DisposeFile( CurrentFile );
                CurrentFile = TempFile.WriteNew( dir, data );
                Changed     = false;
            }
        }

        public const string TmpDirectory = "penumbrametatmp";

        private readonly MetaDefaults                     _default;
        private readonly DirectoryInfo                    _dir;
        private readonly GameResourceManagement           _resourceManagement;
        private readonly Dictionary< GamePath, FileInfo > _resolvedFiles;

        private readonly Dictionary< MetaManipulation, Mod.Mod > _currentManipulations = new();
        private readonly Dictionary< GamePath, FileInformation > _currentFiles         = new();

        public IEnumerable< (MetaManipulation, Mod.Mod) > Manipulations
            => _currentManipulations.Select( kvp => ( kvp.Key, kvp.Value ) );

        public bool TryGetValue( MetaManipulation manip, out Mod.Mod mod )
            => _currentManipulations.TryGetValue( manip, out mod );

        private static void DisposeFile( FileInfo? file )
        {
            file?.Refresh();
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

        private void Reset( bool reload )
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
                _resourceManagement.ReloadPlayerResources();
            }
        }

        public void Reset()
            => Reset( true );

        public void Dispose()
            => Reset();

        private void ClearDirectory()
        {
            _dir.Refresh();
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

        public MetaManager( string name, Dictionary< GamePath, FileInfo > resolvedFiles, DirectoryInfo modDir )
        {
            _resolvedFiles = resolvedFiles;
            _default = Service< MetaDefaults >.Get();
            _resourceManagement = Service< GameResourceManagement >.Get();
            _dir = new DirectoryInfo( Path.Combine( modDir.FullName, TmpDirectory, name.ReplaceInvalidPathSymbols().RemoveNonAsciiSymbols() ) );
            ClearDirectory();
        }

        public void WriteNewFiles()
        {
            Directory.CreateDirectory( _dir.FullName );
            foreach( var kvp in _currentFiles.Where( kvp => kvp.Value.Changed ) )
            {
                kvp.Value.Write( _dir );
                _resolvedFiles[ kvp.Key ] = kvp.Value.CurrentFile!;
            }
        }

        public bool ApplyMod( MetaManipulation m, Mod.Mod mod )
        {
            if( _currentManipulations.ContainsKey( m ) )
            {
                return false;
            }

            _currentManipulations.Add( m, mod );
            var gamePath = m.CorrespondingFilename();
            try
            {
                if( !_currentFiles.TryGetValue( gamePath, out var file ) )
                {
                    file = new FileInformation( _default.CreateNewFile( m ) ?? throw new IOException() )
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
}