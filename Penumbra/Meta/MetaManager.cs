using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Enums;
using Penumbra.Interop;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;
using CharacterUtility = Penumbra.Interop.Structs.CharacterUtility;
using ImcFile = Penumbra.Meta.Files.ImcFile;

namespace Penumbra.Meta;

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
    public readonly Dictionary< Utf8GamePath, ImcFile >    ImcFiles         = new();

    private readonly ModCollection _collection;

    public unsafe void SetFiles()
    {
        foreach( var file in ChangedData )
        {
            Penumbra.CharacterUtility.SetResource( file.Index, ( IntPtr )file.Data, file.Length );
        }
    }

    public bool TryGetValue( MetaManipulation manip, out Mod.Mod? mod )
    {
        mod = manip.ManipulationType switch
        {
            MetaManipulation.Type.Eqp  => EqpManipulations.TryGetValue( manip.Eqp, out var m ) ? m : null,
            MetaManipulation.Type.Gmp  => GmpManipulations.TryGetValue( manip.Gmp, out var m ) ? m : null,
            MetaManipulation.Type.Eqdp => EqdpManipulations.TryGetValue( manip.Eqdp, out var m ) ? m : null,
            MetaManipulation.Type.Est  => EstManipulations.TryGetValue( manip.Est, out var m ) ? m : null,
            MetaManipulation.Type.Rsp  => RspManipulations.TryGetValue( manip.Rsp, out var m ) ? m : null,
            MetaManipulation.Type.Imc  => ImcManipulations.TryGetValue( manip.Imc, out var m ) ? m : null,
            _                          => throw new ArgumentOutOfRangeException(),
        };
        return mod != null;
    }

    public int Count
        => ImcManipulations.Count
          + EqdpManipulations.Count
          + RspManipulations.Count
          + GmpManipulations.Count
          + EstManipulations.Count
          + EqpManipulations.Count;

    public MetaManager2( ModCollection collection )
        => _collection = collection;

    public void ApplyImcFiles( Dictionary< Utf8GamePath, FullPath > resolvedFiles )
    {
        foreach( var path in ImcFiles.Keys )
        {
            resolvedFiles[ path ] = CreateImcPath( path );
        }
    }

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

    private FullPath CreateImcPath( Utf8GamePath path )
    {
        var d = new DirectoryInfo( $":{_collection.Name}/" );
        return new FullPath( d, new Utf8RelPath( path ) );
    }

    public void ResetImc()
    {
        foreach( var (path, file) in ImcFiles )
        {
            _collection.Cache?.ResolvedFiles.Remove( path );
            path.Dispose();
            file.Dispose();
        }

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

        var file = m.Slot switch
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

    public unsafe bool ApplyMod( ImcManipulation m, Mod.Mod mod )
    {
        const uint imcExt = 0x00696D63;

        if( !ImcManipulations.TryAdd( m, mod ) )
        {
            return false;
        }

        var path = m.GamePath();
        if( !ImcFiles.TryGetValue( path, out var file ) )
        {
            file = new ImcFile( path );
        }

        if( !m.Apply( file ) )
        {
            return false;
        }

        ImcFiles[ path ] = file;
        var fullPath = CreateImcPath( path );
        if( _collection.Cache != null )
        {
            _collection.Cache.ResolvedFiles[ path ] = fullPath;
        }

        var resource = ResourceLoader.FindResource( ResourceCategory.Chara, imcExt, ( uint )path.Path.Crc32 );
        if( resource != null )
        {
            file.Replace( ( ResourceHandle* )resource );
        }

        return true;
    }

    public static unsafe byte ImcHandler( Utf8GamePath gamePath, ResourceManager* resourceManager,
        SeFileDescriptor* fileDescriptor, int priority, bool isSync )
    {
        var split = gamePath.Path.Split( ( byte )'|', 2, true );
        fileDescriptor->ResourceHandle->FileNameData   = split[ 1 ].Path;
        fileDescriptor->ResourceHandle->FileNameLength = split[ 1 ].Length;

        var ret = Penumbra.ResourceLoader.ReadSqPackHook.Original( resourceManager, fileDescriptor, priority, isSync );
        if( Penumbra.ModManager.Collections.Collections.TryGetValue( split[ 0 ].ToString(), out var collection )
        && collection.Cache != null
        && collection.Cache.MetaManipulations.ImcFiles.TryGetValue(
               Utf8GamePath.FromSpan( split[ 1 ].Span, out var p, false ) ? p : Utf8GamePath.Empty, out var file ) )
        {
            file.Replace( fileDescriptor->ResourceHandle );
        }

        fileDescriptor->ResourceHandle->FileNameData   = gamePath.Path.Path;
        fileDescriptor->ResourceHandle->FileNameLength = gamePath.Path.Length;
        return ret;
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
            _                          => throw new ArgumentOutOfRangeException(),
        };
    }
}