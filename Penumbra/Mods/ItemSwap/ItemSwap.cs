using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Penumbra.GameData.Files;
using Penumbra.String.Classes;

namespace Penumbra.Mods.ItemSwap;

public static class ItemSwap
{
    public class InvalidItemTypeException : Exception
    { }

    public class InvalidImcException : Exception
    { }

    public class IdUnavailableException : Exception
    { }

    private static bool LoadFile( FullPath path, out byte[] data )
    {
        if( path.FullName.Length > 0 )
        {
            try
            {
                if( path.IsRooted )
                {
                    data = File.ReadAllBytes( path.FullName );
                    return true;
                }

                var file = Dalamud.GameData.GetFile( path.InternalName.ToString() );
                if( file != null )
                {
                    data = file.Data;
                    return true;
                }
            }
            catch( Exception e )
            {
                Penumbra.Log.Debug( $"Could not load file {path}:\n{e}" );
            }
        }

        data = Array.Empty< byte >();
        return false;
    }

    public class GenericFile : IWritable
    {
        public readonly byte[] Data;
        public bool Valid { get; }

        public GenericFile( FullPath path )
            => Valid = LoadFile( path, out Data );

        public byte[] Write()
            => Data;

        public static readonly GenericFile Invalid = new(FullPath.Empty);
    }

    public static bool LoadFile( FullPath path, [NotNullWhen( true )] out GenericFile? file )
    {
        file = new GenericFile( path );
        if( file.Valid )
        {
            return true;
        }

        file = null;
        return false;
    }

    public static bool LoadMdl( FullPath path, [NotNullWhen( true )] out MdlFile? file )
    {
        try
        {
            if( LoadFile( path, out byte[] data ) )
            {
                file = new MdlFile( data );
                return true;
            }
        }
        catch( Exception e )
        {
            Penumbra.Log.Debug( $"Could not parse file {path} to Mdl:\n{e}" );
        }

        file = null;
        return false;
    }

    public static bool LoadMtrl( FullPath path, [NotNullWhen( true )] out MtrlFile? file )
    {
        try
        {
            if( LoadFile( path, out byte[] data ) )
            {
                file = new MtrlFile( data );
                return true;
            }
        }
        catch( Exception e )
        {
            Penumbra.Log.Debug( $"Could not parse file {path} to Mtrl:\n{e}" );
        }

        file = null;
        return false;
    }
}