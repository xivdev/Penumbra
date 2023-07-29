using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Import;

public partial class TexToolsMeta
{
    public static void WriteTexToolsMeta(MetaFileManager manager, IEnumerable<MetaManipulation> manipulations, DirectoryInfo basePath)
    {
        var files = ConvertToTexTools(manager, manipulations);

        foreach (var (file, data) in files)
        {
            var path = Path.Combine(basePath.FullName, file);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, data);
            }
            catch (Exception e)
            {
                Penumbra.Log.Error($"Could not write meta file {path}:\n{e}");
            }
        }
    }

    public static Dictionary< string, byte[] > ConvertToTexTools( MetaFileManager manager, IEnumerable< MetaManipulation > manips )
    {
        var ret = new Dictionary< string, byte[] >();
        foreach( var group in manips.GroupBy( ManipToPath ) )
        {
            if( group.Key.Length == 0 )
            {
                continue;
            }

            var bytes = group.Key.EndsWith( ".rgsp" )
                ? WriteRgspFile( manager, group.Key, group )
                : WriteMetaFile( manager, group.Key, group );
            if( bytes.Length == 0 )
            {
                continue;
            }

            ret.Add( group.Key, bytes );
        }

        return ret;
    }

    private static byte[] WriteRgspFile( MetaFileManager manager, string path, IEnumerable< MetaManipulation > manips )
    {
        var       list = manips.GroupBy( m => m.Rsp.Attribute ).ToDictionary( m => m.Key, m => m.Last().Rsp );
        using var m    = new MemoryStream( 45 );
        using var b    = new BinaryWriter( m );
        // Version
        b.Write( byte.MaxValue );
        b.Write( ( ushort )2 );

        var race   = list.First().Value.SubRace;
        var gender = list.First().Value.Attribute.ToGender();
        b.Write( ( byte )(race   - 1) ); // offset by one due to Unknown
        b.Write( ( byte )(gender - 1) ); // offset by one due to Unknown

        void Add( params RspAttribute[] attributes )
        {
            foreach( var attribute in attributes )
            {
                var value = list.TryGetValue( attribute, out var tmp ) ? tmp.Entry : CmpFile.GetDefault( manager, race, attribute );
                b.Write( value );
            }
        }

        if( gender == Gender.Male )
        {
            Add( RspAttribute.MaleMinSize, RspAttribute.MaleMaxSize, RspAttribute.MaleMinTail, RspAttribute.MaleMaxTail );
        }
        else
        {
            Add( RspAttribute.FemaleMinSize, RspAttribute.FemaleMaxSize, RspAttribute.FemaleMinTail, RspAttribute.FemaleMaxTail );
            Add( RspAttribute.BustMinX, RspAttribute.BustMinY, RspAttribute.BustMinZ, RspAttribute.BustMaxX, RspAttribute.BustMaxY, RspAttribute.BustMaxZ );
        }

        return m.GetBuffer();
    }

    private static byte[] WriteMetaFile( MetaFileManager manager, string path, IEnumerable< MetaManipulation > manips )
    {
        var filteredManips = manips.GroupBy( m => m.ManipulationType ).ToDictionary( p => p.Key, p => p.Select( x => x ) );

        using var m = new MemoryStream();
        using var b = new BinaryWriter( m );

        // Header
        // Current TT Metadata version.
        b.Write( 2u );

        // Null-terminated ASCII path.
        var utf8Path = Encoding.ASCII.GetBytes( path );
        b.Write( utf8Path );
        b.Write( ( byte )0 );

        // Number of Headers
        b.Write( ( uint )filteredManips.Count );
        // Current TT Size of Headers
        b.Write( ( uint )12 );

        // Start of Header Entries for some reason, which is absolutely useless.
        var headerStart = b.BaseStream.Position + 4;
        b.Write( ( uint )headerStart );

        var offset = ( uint )( b.BaseStream.Position + 12 * filteredManips.Count );
        foreach( var (header, data) in filteredManips )
        {
            b.Write( ( uint )header );
            b.Write( offset );

            var size = WriteData( manager, b, offset, header, data );
            b.Write( size );
            offset += size;
        }

        return m.ToArray();
    }

    private static uint WriteData( MetaFileManager manager, BinaryWriter b, uint offset, MetaManipulation.Type type, IEnumerable< MetaManipulation > manips )
    {
        var oldPos = b.BaseStream.Position;
        b.Seek( ( int )offset, SeekOrigin.Begin );

        switch( type )
        {
            case MetaManipulation.Type.Imc:
                var allManips = manips.ToList();
                var baseFile  = new ImcFile( manager, allManips[ 0 ].Imc );
                foreach( var manip in allManips )
                {
                    manip.Imc.Apply( baseFile );
                }

                var partIdx = allManips[ 0 ].Imc.ObjectType is ObjectType.Equipment or ObjectType.Accessory
                    ? ImcFile.PartIndex( allManips[ 0 ].Imc.EquipSlot )
                    : 0;

                for( var i = 0; i <= baseFile.Count; ++i )
                {
                    var entry = baseFile.GetEntry( partIdx, (Variant)i );
                    b.Write( entry.MaterialId );
                    b.Write( entry.DecalId );
                    b.Write( entry.AttributeAndSound );
                    b.Write( entry.VfxId );
                    b.Write( entry.MaterialAnimationId );
                }

                break;
            case MetaManipulation.Type.Eqdp:
                foreach( var manip in manips )
                {
                    b.Write( ( uint )Names.CombinedRace( manip.Eqdp.Gender, manip.Eqdp.Race ) );
                    var entry = ( byte )(( ( uint )manip.Eqdp.Entry >> Eqdp.Offset( manip.Eqdp.Slot ) ) & 0x03);
                    b.Write( entry );
                }

                break;
            case MetaManipulation.Type.Eqp:
                foreach( var manip in manips )
                {
                    var bytes = BitConverter.GetBytes( (ulong) manip.Eqp.Entry );
                    var (numBytes, byteOffset) = Eqp.BytesAndOffset( manip.Eqp.Slot );
                    for( var i = byteOffset; i < numBytes + byteOffset; ++i )
                        b.Write( bytes[ i ] );
                }

                break;
            case MetaManipulation.Type.Est:
                foreach( var manip in manips )
                {
                    b.Write( ( ushort )Names.CombinedRace( manip.Est.Gender, manip.Est.Race ) );
                    b.Write( manip.Est.SetId.Id );
                    b.Write( manip.Est.Entry );
                }

                break;
            case MetaManipulation.Type.Gmp:
                foreach( var manip in manips )
                {
                    b.Write( ( uint )manip.Gmp.Entry.Value );
                    b.Write( manip.Gmp.Entry.UnknownTotal );
                }

                break;
        }

        var size = b.BaseStream.Position - offset;
        b.Seek( ( int )oldPos, SeekOrigin.Begin );
        return ( uint )size;
    }

    private static string ManipToPath( MetaManipulation manip )
        => manip.ManipulationType switch
        {
            MetaManipulation.Type.Imc  => ManipToPath( manip.Imc ),
            MetaManipulation.Type.Eqdp => ManipToPath( manip.Eqdp ),
            MetaManipulation.Type.Eqp  => ManipToPath( manip.Eqp ),
            MetaManipulation.Type.Est  => ManipToPath( manip.Est ),
            MetaManipulation.Type.Gmp  => ManipToPath( manip.Gmp ),
            MetaManipulation.Type.Rsp  => ManipToPath( manip.Rsp ),
            _                          => string.Empty,
        };

    private static string ManipToPath( ImcManipulation manip )
    {
        var path = manip.GamePath().ToString();
        var replacement = manip.ObjectType switch
        {
            ObjectType.Accessory => $"_{manip.EquipSlot.ToSuffix()}.meta",
            ObjectType.Equipment => $"_{manip.EquipSlot.ToSuffix()}.meta",
            ObjectType.Character => $"_{manip.BodySlot.ToSuffix()}.meta",
            _                    => ".meta",
        };

        return path.Replace( ".imc", replacement );
    }

    private static string ManipToPath( EqdpManipulation manip )
        => manip.Slot.IsAccessory()
            ? $"chara/accessory/a{manip.SetId:D4}/a{manip.SetId:D4}_{manip.Slot.ToSuffix()}.meta"
            : $"chara/equipment/e{manip.SetId:D4}/e{manip.SetId:D4}_{manip.Slot.ToSuffix()}.meta";

    private static string ManipToPath( EqpManipulation manip )
        => manip.Slot.IsAccessory()
            ? $"chara/accessory/a{manip.SetId:D4}/a{manip.SetId:D4}_{manip.Slot.ToSuffix()}.meta"
            : $"chara/equipment/e{manip.SetId:D4}/e{manip.SetId:D4}_{manip.Slot.ToSuffix()}.meta";

    private static string ManipToPath( EstManipulation manip )
    {
        var raceCode = Names.CombinedRace( manip.Gender, manip.Race ).ToRaceCode();
        return manip.Slot switch
        {
            EstManipulation.EstType.Hair => $"chara/human/c{raceCode}/obj/hair/h{manip.SetId:D4}/c{raceCode}h{manip.SetId:D4}_hir.meta",
            EstManipulation.EstType.Face => $"chara/human/c{raceCode}/obj/face/h{manip.SetId:D4}/c{raceCode}f{manip.SetId:D4}_fac.meta",
            EstManipulation.EstType.Body => $"chara/equipment/e{manip.SetId:D4}/e{manip.SetId:D4}_{EquipSlot.Body.ToSuffix()}.meta",
            EstManipulation.EstType.Head => $"chara/equipment/e{manip.SetId:D4}/e{manip.SetId:D4}_{EquipSlot.Head.ToSuffix()}.meta",
            _                            => throw new ArgumentOutOfRangeException(),
        };
    }

    private static string ManipToPath( GmpManipulation manip )
        => $"chara/equipment/e{manip.SetId:D4}/e{manip.SetId:D4}_{EquipSlot.Head.ToSuffix()}.meta";


    private static string ManipToPath( RspManipulation manip )
        => $"chara/xls/charamake/rgsp/{( int )manip.SubRace - 1}-{( int )manip.Attribute.ToGender() - 1}.rgsp";
}