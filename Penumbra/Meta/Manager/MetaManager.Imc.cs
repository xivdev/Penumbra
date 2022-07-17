using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using OtterGui.Filesystem;
using Penumbra.Collections;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Meta.Manager;

public partial class MetaManager
{
    private readonly Dictionary< Utf8GamePath, ImcFile > _imcFiles         = new();
    private readonly List< ImcManipulation >             _imcManipulations = new();
    private static   int                                 _imcManagerCount;

    public void SetImcFiles()
    {
        if( !_collection.HasCache )
        {
            return;
        }

        foreach( var path in _imcFiles.Keys )
        {
            _collection.ForceFile( path, CreateImcPath( path ) );
        }
    }

    public void ResetImc()
    {
        if( _collection.HasCache )
        {
            foreach( var (path, file) in _imcFiles )
            {
                _collection.RemoveFile( path );
                file.Reset();
            }
        }
        else
        {
            foreach( var (_, file) in _imcFiles )
            {
                file.Reset();
            }
        }

        _imcManipulations.Clear();
    }

    public bool ApplyMod( ImcManipulation manip )
    {
        _imcManipulations.AddOrReplace( manip );
        var path = manip.GamePath();
        try
        {
            if( !_imcFiles.TryGetValue( path, out var file ) )
            {
                file = new ImcFile( path );
            }

            if( !manip.Apply( file ) )
            {
                return false;
            }

            _imcFiles[ path ] = file;
            var fullPath = CreateImcPath( path );
            if( _collection.HasCache )
            {
                _collection.ForceFile( path, fullPath );
            }

            return true;
        }
        catch( Exception e )
        {
            ++Penumbra.ImcExceptions;
            PluginLog.Error( $"Could not apply IMC Manipulation:\n{e}" );
            return false;
        }
    }

    public bool RevertMod( ImcManipulation m )
    {
        if( !_imcManipulations.Remove( m ) )
        {
            return false;
        }

        var path = m.GamePath();
        if( !_imcFiles.TryGetValue( path, out var file ) )
        {
            return false;
        }

        var def   = ImcFile.GetDefault( path, m.EquipSlot, m.Variant, out _ );
        var manip = m with { Entry = def };
        if( !manip.Apply( file ) )
        {
            return false;
        }

        var fullPath = CreateImcPath( path );
        if( _collection.HasCache )
        {
            _collection.ForceFile( path, fullPath );
        }

        return true;
    }

    public void DisposeImc()
    {
        foreach( var file in _imcFiles.Values )
        {
            file.Dispose();
        }

        _imcFiles.Clear();
        _imcManipulations.Clear();
        RestoreImcDelegate();
    }

    private static unsafe void SetupImcDelegate()
    {
        if( _imcManagerCount++ == 0 )
        {
            Penumbra.ResourceLoader.ResourceLoadCustomization += ImcLoadHandler;
        }
    }

    private static unsafe void RestoreImcDelegate()
    {
        if( --_imcManagerCount == 0 )
        {
            Penumbra.ResourceLoader.ResourceLoadCustomization -= ImcLoadHandler;
        }
    }

    private FullPath CreateImcPath( Utf8GamePath path )
        => new($"|{_collection.Name}_{_collection.ChangeCounter}|{path}");


    private static unsafe bool ImcLoadHandler( Utf8String split, Utf8String path, ResourceManager* resourceManager,
        SeFileDescriptor* fileDescriptor, int priority, bool isSync, out byte ret )
    {
        ret = 0;
        if( fileDescriptor->ResourceHandle->FileType != ResourceType.Imc )
        {
            return false;
        }

        PluginLog.Verbose( "Using ImcLoadHandler for path {$Path:l}.", path );
        ret = Penumbra.ResourceLoader.ReadSqPackHook.Original( resourceManager, fileDescriptor, priority, isSync );

        var lastUnderscore = split.LastIndexOf( ( byte )'_' );
        var name           = lastUnderscore == -1 ? split.ToString() : split.Substring( 0, lastUnderscore ).ToString();
        if( ( Penumbra.TempMods.CollectionByName( name, out var collection )
            || Penumbra.CollectionManager.ByName( name, out collection ) )
        && collection.HasCache
        && collection.MetaCache!._imcFiles.TryGetValue( Utf8GamePath.FromSpan( path.Span, out var p ) ? p : Utf8GamePath.Empty, out var file ) )
        {
            PluginLog.Debug( "Loaded {GamePath:l} from file and replaced with IMC from collection {Collection:l}.", path,
                collection.AnonymizedName );
            file.Replace( fileDescriptor->ResourceHandle );
        }

        return true;
    }
}