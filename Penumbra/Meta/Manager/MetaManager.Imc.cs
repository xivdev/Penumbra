using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using OtterGui.Filesystem;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;

namespace Penumbra.Meta.Manager;

public partial class MetaManager
{
    private readonly Dictionary< Utf8GamePath, ImcFile > _imcFiles         = new();
    private readonly List< ImcManipulation >             _imcManipulations = new();

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
                file = new ImcFile( manip );
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
        catch( ImcException e )
        {
            Penumbra.ValidityChecker.ImcExceptions.Add( e );
            Penumbra.Log.Error( e.ToString() );
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Could not apply IMC Manipulation {manip}:\n{e}" );
        }

        return false;
    }

    public bool RevertMod( ImcManipulation m )
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

        var def   = ImcFile.GetDefault( path, m.EquipSlot, m.Variant, out _ );
        var manip = m.Copy( def );
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
    }

    private FullPath CreateImcPath( Utf8GamePath path )
        => new($"|{_collection.Name}_{_collection.ChangeCounter}|{path}");

    public bool GetImcFile(Utf8GamePath path, [NotNullWhen(true)] out ImcFile? file)
        => _imcFiles.TryGetValue(path, out file);
}