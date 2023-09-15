using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;

namespace Penumbra.Mods.ItemSwap;

public static class ItemSwap
{
    public class InvalidItemTypeException : Exception
    { }

    public class MissingFileException : Exception
    {
        public readonly ResourceType Type;

        public MissingFileException( ResourceType type, object path )
            : base($"Could not load {type} File Data for \"{path}\".")
            => Type = type;
    }

    private static bool LoadFile( MetaFileManager manager, FullPath path, out byte[] data )
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

                var file = manager.GameData.GetFile( path.InternalName.ToString() );
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

        public GenericFile( MetaFileManager manager, FullPath path )
            => Valid = LoadFile( manager, path, out Data );

        public byte[] Write()
            => Data;

        public static readonly GenericFile Invalid = new(null!, FullPath.Empty);
    }

    public static bool LoadFile( MetaFileManager manager, FullPath path, [NotNullWhen( true )] out GenericFile? file )
    {
        file = new GenericFile( manager, path );
        if( file.Valid )
        {
            return true;
        }

        file = null;
        return false;
    }

    public static bool LoadMdl( MetaFileManager manager, FullPath path, [NotNullWhen( true )] out MdlFile? file )
    {
        try
        {
            if( LoadFile( manager, path, out byte[] data ) )
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

    public static bool LoadMtrl(MetaFileManager manager, FullPath path, [NotNullWhen( true )] out MtrlFile? file )
    {
        try
        {
            if( LoadFile( manager, path, out byte[] data ) )
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

    public static bool LoadAvfx( MetaFileManager manager, FullPath path, [NotNullWhen( true )] out AvfxFile? file )
    {
        try
        {
            if( LoadFile( manager, path, out byte[] data ) )
            {
                file = new AvfxFile( data );
                return true;
            }
        }
        catch( Exception e )
        {
            Penumbra.Log.Debug( $"Could not parse file {path} to Avfx:\n{e}" );
        }

        file = null;
        return false;
    }


    public static FileSwap CreatePhyb(MetaFileManager manager, Func< Utf8GamePath, FullPath > redirections, EstManipulation.EstType type, GenderRace race, ushort estEntry )
    {
        var phybPath = GamePaths.Skeleton.Phyb.Path( race, EstManipulation.ToName( type ), estEntry );
        return FileSwap.CreateSwap( manager, ResourceType.Phyb, redirections, phybPath, phybPath );
    }

    public static FileSwap CreateSklb(MetaFileManager manager, Func< Utf8GamePath, FullPath > redirections, EstManipulation.EstType type, GenderRace race, ushort estEntry )
    {
        var sklbPath = GamePaths.Skeleton.Sklb.Path( race, EstManipulation.ToName( type ), estEntry );
        return FileSwap.CreateSwap(manager, ResourceType.Sklb, redirections, sklbPath, sklbPath );
    }

    /// <remarks> metaChanges is not manipulated, but IReadOnlySet does not support TryGetValue. </remarks>
    public static MetaSwap? CreateEst( MetaFileManager manager, Func< Utf8GamePath, FullPath > redirections, Func< MetaManipulation, MetaManipulation > manips, EstManipulation.EstType type,
        GenderRace genderRace, SetId idFrom, SetId idTo, bool ownMdl )
    {
        if( type == 0 )
        {
            return null;
        }

        var (gender, race) = genderRace.Split();
        var fromDefault = new EstManipulation( gender, race, type, idFrom, EstFile.GetDefault( manager, type, genderRace, idFrom ) );
        var toDefault   = new EstManipulation( gender, race, type, idTo, EstFile.GetDefault( manager, type, genderRace, idTo ) );
        var est         = new MetaSwap( manips, fromDefault, toDefault );

        if( ownMdl && est.SwapApplied.Est.Entry >= 2 )
        {
            var phyb = CreatePhyb( manager, redirections, type, genderRace, est.SwapApplied.Est.Entry );
            est.ChildSwaps.Add( phyb );
            var sklb = CreateSklb( manager, redirections, type, genderRace, est.SwapApplied.Est.Entry );
            est.ChildSwaps.Add( sklb );
        }
        else if( est.SwapAppliedIsDefault )
        {
            return null;
        }

        return est;
    }

    public static int GetStableHashCode( this string str )
    {
        unchecked
        {
            var hash1 = 5381;
            var hash2 = hash1;

            for( var i = 0; i < str.Length && str[ i ] != '\0'; i += 2 )
            {
                hash1 = ( ( hash1 << 5 ) + hash1 ) ^ str[ i ];
                if( i == str.Length - 1 || str[ i + 1 ] == '\0' )
                {
                    break;
                }

                hash2 = ( ( hash2 << 5 ) + hash2 ) ^ str[ i + 1 ];
            }

            return hash1 + hash2 * 1566083941;
        }
    }

    public static string ReplaceAnyId( string path, char idType, SetId id, bool condition = true )
        => condition
            ? Regex.Replace( path, $"{idType}\\d{{4}}", $"{idType}{id.Id:D4}" )
            : path;

    public static string ReplaceAnyRace( string path, GenderRace to, bool condition = true )
        => ReplaceAnyId( path, 'c', ( ushort )to, condition );

    public static string ReplaceAnyBody( string path, BodySlot slot, SetId to, bool condition = true )
        => ReplaceAnyId( path, slot.ToAbbreviation(), to, condition );

    public static string ReplaceId( string path, char type, SetId idFrom, SetId idTo, bool condition = true )
        => condition
            ? path.Replace( $"{type}{idFrom.Id:D4}", $"{type}{idTo.Id:D4}" )
            : path;

    public static string ReplaceSlot( string path, EquipSlot from, EquipSlot to, bool condition = true )
        => condition
            ? path.Replace( $"_{from.ToSuffix()}_", $"_{to.ToSuffix()}_" )
            : path;

    public static string ReplaceRace( string path, GenderRace from, GenderRace to, bool condition = true )
        => ReplaceId( path, 'c', ( ushort )from, ( ushort )to, condition );

    public static string ReplaceBody( string path, BodySlot slot, SetId idFrom, SetId idTo, bool condition = true )
        => ReplaceId( path, slot.ToAbbreviation(), idFrom, idTo, condition );

    public static string AddSuffix( string path, string ext, string suffix, bool condition = true )
        => condition
            ? path.Replace( ext, suffix + ext )
            : path;
}