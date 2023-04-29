using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using OtterGui.Filesystem;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;

namespace Penumbra.Collections.Cache;

public readonly struct ImcCache : IDisposable
{
    private readonly Dictionary< Utf8GamePath, ImcFile > _imcFiles         = new();
    private readonly List< ImcManipulation >             _imcManipulations = new();

    public ImcCache()
    { }

    public void SetFiles(ModCollection collection)
    {
        foreach( var path in _imcFiles.Keys )
            collection._cache!.ForceFile( path, CreateImcPath( collection, path ) );
    }

    public void Reset(ModCollection collection)
    {
        foreach( var (path, file) in _imcFiles )
        {
            collection._cache!.RemoveFile( path );
            file.Reset();
        }

        _imcManipulations.Clear();
    }

    public bool ApplyMod( MetaFileManager manager, ModCollection collection, ImcManipulation manip )
    {
        if( !manip.Valid )
        {
            return false;
        }

        _imcManipulations.AddOrReplace( manip );
        var path = manip.GamePath();
        try
        {
            if( !_imcFiles.TryGetValue( path, out var file ) )
            {
                file = new ImcFile( manager, manip );
            }

            if( !manip.Apply( file ) )
            {
                return false;
            }

            _imcFiles[ path ] = file;
            var fullPath = CreateImcPath( collection, path );
            collection._cache!.ForceFile( path, fullPath );

            return true;
        }
        catch( ImcException e )
        {
            manager.ValidityChecker.ImcExceptions.Add( e );
            Penumbra.Log.Error( e.ToString() );
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Could not apply IMC Manipulation {manip}:\n{e}" );
        }

        return false;
    }

    public bool RevertMod( MetaFileManager manager, ModCollection collection, ImcManipulation m )
    {
        if( !m.Valid || !_imcManipulations.Remove( m ) )
        {
            return false;
        }

        var path = m.GamePath();
        if( !_imcFiles.TryGetValue( path, out var file ) )
        {
            return false;
        }

        var def   = ImcFile.GetDefault( manager, path, m.EquipSlot, m.Variant, out _ );
        var manip = m.Copy( def );
        if( !manip.Apply( file ) )
            return false;

        var fullPath = CreateImcPath( collection, path );
        collection._cache!.ForceFile( path, fullPath );

        return true;
    }

    public void Dispose()
    {
        foreach( var file in _imcFiles.Values )
            file.Dispose();

        _imcFiles.Clear();
        _imcManipulations.Clear();
    }

    private static FullPath CreateImcPath( ModCollection collection, Utf8GamePath path )
        => new($"|{collection.Name}_{collection.ChangeCounter}|{path}");

    public bool GetImcFile(Utf8GamePath path, [NotNullWhen(true)] out ImcFile? file)
        => _imcFiles.TryGetValue(path, out file);
}