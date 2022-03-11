using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Lumina.Data.Files;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Util;

namespace Penumbra.Meta;

public struct TemporaryImcFile : IDisposable
{

    public void Dispose()
    {

    }
}

public class MetaManager2 : IDisposable
{
    public readonly List< MetaBaseFile > ChangedData = new(7 + CharacterUtility.NumEqdpFiles);

    public ExpandedEqpFile?    EqpFile;
    public ExpandedGmpFile?    GmpFile;
    public ExpandedEqdpFile?[] EqdpFile = new ExpandedEqdpFile?[CharacterUtility.NumEqdpFiles];
    public EstFile?            FaceEstFile;
    public EstFile?            HairEstFile;
    public EstFile?            BodyEstFile;
    public EstFile?            HeadEstFile;
    public CmpFile?            CmpFile;

    public readonly Dictionary< EqpManipulation, Mod.Mod >  EqpManipulations  = new();
    public readonly Dictionary< EstManipulation, Mod.Mod >  EstManipulations  = new();
    public readonly Dictionary< GmpManipulation, Mod.Mod >  GmpManipulations  = new();
    public readonly Dictionary< RspManipulation, Mod.Mod >  RspManipulations  = new();
    public readonly Dictionary< EqdpManipulation, Mod.Mod > EqdpManipulations = new();

    public readonly Dictionary< ImcManipulation, Mod.Mod > ImcManipulations = new();
    public readonly List< TemporaryImcFile >               ImcFiles         = new();

    public void ResetEqp()
    {
        if( EqpFile != null )
        {
            EqpFile.Reset( EqpManipulations.Keys.Select( m => ( int )m.SetId ) );
            EqpManipulations.Clear();
            ChangedData.Remove( EqpFile );
        }
    }

    public void ResetGmp()
    {
        if( GmpFile != null )
        {
            GmpFile.Reset( GmpManipulations.Keys.Select( m => ( int )m.SetId ) );
            GmpManipulations.Clear();
            ChangedData.Remove( GmpFile );
        }
    }

    public void ResetCmp()
    {
        if( CmpFile != null )
        {
            CmpFile.Reset( RspManipulations.Keys.Select( m => ( m.SubRace, m.Attribute ) ) );
            RspManipulations.Clear();
            ChangedData.Remove( CmpFile );
        }
    }

    public void ResetEst()
    {
        FaceEstFile?.Reset();
        HairEstFile?.Reset();
        BodyEstFile?.Reset();
        HeadEstFile?.Reset();
        RspManipulations.Clear();
        ChangedData.RemoveAll( f => f is EstFile );
    }

    public void ResetEqdp()
    {
        foreach( var file in EqdpFile )
        {
            file?.Reset( EqdpManipulations.Keys.Where( m => m.FileIndex() == file.Index ).Select( m => ( int )m.SetId ) );
        }

        ChangedData.RemoveAll( f => f is ExpandedEqdpFile );
        EqdpManipulations.Clear();
    }

    public void ResetImc()
    {
        foreach( var file in ImcFiles )
            file.Dispose();
        ImcFiles.Clear();
        ImcManipulations.Clear();
    }

    public void Reset()
    {
        ChangedData.Clear();
        ResetEqp();
        ResetGmp();
        ResetCmp();
        ResetEst();
        ResetEqdp();
        ResetImc();
    }

    private static void Dispose< T >( ref T? file ) where T : class, IDisposable
    {
        if( file != null )
        {
            file.Dispose();
            file = null;
        }
    }

    public void Dispose()
    {
        ChangedData.Clear();
        EqpManipulations.Clear();
        EstManipulations.Clear();
        GmpManipulations.Clear();
        RspManipulations.Clear();
        EqdpManipulations.Clear();
        Dispose( ref EqpFile );
        Dispose( ref GmpFile );
        Dispose( ref FaceEstFile );
        Dispose( ref HairEstFile );
        Dispose( ref BodyEstFile );
        Dispose( ref HeadEstFile );
        Dispose( ref CmpFile );
        for( var i = 0; i < CharacterUtility.NumEqdpFiles; ++i )
        {
            Dispose( ref EqdpFile[ i ] );
        }

        ResetImc();
    }

    private void AddFile( MetaBaseFile file )
    {
        if( !ChangedData.Contains( file ) )
        {
            ChangedData.Add( file );
        }
    }


    public bool ApplyMod( EqpManipulation m, Mod.Mod mod )
    {
        if( !EqpManipulations.TryAdd( m, mod ) )
        {
            return false;
        }

        EqpFile ??= new ExpandedEqpFile();
        if( !m.Apply( EqpFile ) )
        {
            return false;
        }

        AddFile( EqpFile );
        return true;
    }

    public bool ApplyMod( GmpManipulation m, Mod.Mod mod )
    {
        if( !GmpManipulations.TryAdd( m, mod ) )
        {
            return false;
        }

        GmpFile ??= new ExpandedGmpFile();
        if( !m.Apply( GmpFile ) )
        {
            return false;
        }

        AddFile( GmpFile );
        return true;
    }

    public bool ApplyMod( EstManipulation m, Mod.Mod mod )
    {
        if( !EstManipulations.TryAdd( m, mod ) )
        {
            return false;
        }

        var file = m.Type switch
        {
            EstManipulation.EstType.Hair => HairEstFile ??= new EstFile( EstManipulation.EstType.Hair ),
            EstManipulation.EstType.Face => FaceEstFile ??= new EstFile( EstManipulation.EstType.Face ),
            EstManipulation.EstType.Body => BodyEstFile ??= new EstFile( EstManipulation.EstType.Body ),
            EstManipulation.EstType.Head => HeadEstFile ??= new EstFile( EstManipulation.EstType.Head ),
            _                            => throw new ArgumentOutOfRangeException(),
        };
        if( !m.Apply( file ) )
        {
            return false;
        }

        AddFile( file );
        return true;
    }

    public bool ApplyMod( RspManipulation m, Mod.Mod mod )
    {
        if( !RspManipulations.TryAdd( m, mod ) )
        {
            return false;
        }

        CmpFile ??= new CmpFile();
        if( !m.Apply( CmpFile ) )
        {
            return false;
        }

        AddFile( CmpFile );
        return true;
    }

    public bool ApplyMod( EqdpManipulation m, Mod.Mod mod )
    {
        if( !EqdpManipulations.TryAdd( m, mod ) )
        {
            return false;
        }

        var file = EqdpFile[ m.FileIndex() - 2 ] ??= new ExpandedEqdpFile( Names.CombinedRace( m.Gender, m.Race ), m.Slot.IsAccessory() );
        if( !m.Apply( file ) )
        {
            return false;
        }

        AddFile( file );
        return true;
    }

    public bool ApplyMod( ImcManipulation m, Mod.Mod mod )
    {
        if( !ImcManipulations.TryAdd( m, mod ) )
        {
            return false;
        }

        return true;
    }

    public bool ApplyMod( MetaManipulation m, Mod.Mod mod )
    {
        return m.ManipulationType switch
        {
            MetaManipulation.Type.Eqp  => ApplyMod( m.Eqp, mod ),
            MetaManipulation.Type.Gmp  => ApplyMod( m.Gmp, mod ),
            MetaManipulation.Type.Eqdp => ApplyMod( m.Eqdp, mod ),
            MetaManipulation.Type.Est  => ApplyMod( m.Est, mod ),
            MetaManipulation.Type.Rsp  => ApplyMod( m.Rsp, mod ),
            MetaManipulation.Type.Imc  => ApplyMod( m.Imc, mod ),
            _                          => throw new ArgumentOutOfRangeException()
        };
    }
}

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
                ImcFile imc   => imc.WriteBytes(),
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
        }
    }
}