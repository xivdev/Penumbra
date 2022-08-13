using System;
using System.IO;
using System.Text;
using Lumina.Data.Parsing;
using Lumina.Extensions;

namespace Penumbra.GameData.Files;

public partial class MtrlFile
{
    public struct ColorSet
    {
        public string Name;
        public ushort Index;
    }

    public struct Texture
    {
        public string Path;
        public ushort Flags;
    }

    public struct Constant
    {
        public uint Id;
        public uint Value;
    }

    public struct ShaderPackageData
    {
        public string      Name;
        public ShaderKey[] ShaderKeys;
        public Constant[]  Constants;
        public Sampler[]   Samplers;
        public float[]     ShaderValues;
        public uint        Unk;
    }


    public uint Version;

    public Texture[]         Textures;
    public ColorSet[]        UvColorSets;
    public ColorSet[]        ColorSets;
    public ushort[]          ColorSetData;
    public ShaderPackageData ShaderPackage;
    public byte[]            AdditionalData;

    public MtrlFile( byte[] data )
    {
        using var stream = new MemoryStream( data );
        using var r      = new BinaryReader( stream );

        Version = r.ReadUInt32();
        r.ReadUInt16(); // file size
        var dataSetSize             = r.ReadUInt16();
        var stringTableSize         = r.ReadUInt16();
        var shaderPackageNameOffset = r.ReadUInt16();
        var textureCount            = r.ReadByte();
        var uvSetCount              = r.ReadByte();
        var colorSetCount           = r.ReadByte();
        var additionalDataSize      = r.ReadByte();

        Textures    = ReadTextureOffsets( r, textureCount, out var textureOffsets );
        UvColorSets = ReadColorSetOffsets( r, uvSetCount, out var uvOffsets );
        ColorSets   = ReadColorSetOffsets( r, colorSetCount, out var colorOffsets );

        var strings = r.ReadBytes( stringTableSize );
        for( var i = 0; i < textureCount; ++i )
        {
            Textures[ i ].Path = UseOffset( strings, textureOffsets[ i ] );
        }

        for( var i = 0; i < uvSetCount; ++i )
        {
            UvColorSets[ i ].Name = UseOffset( strings, uvOffsets[ i ] );
        }

        for( var i = 0; i < colorSetCount; ++i )
        {
            ColorSets[ i ].Name = UseOffset( strings, colorOffsets[ i ] );
        }

        ShaderPackage.Name = UseOffset( strings, shaderPackageNameOffset );

        AdditionalData = r.ReadBytes( additionalDataSize );
        ColorSetData   = r.ReadStructuresAsArray< ushort >( dataSetSize / 2 );

        var shaderValueListSize = r.ReadUInt16();
        var shaderKeyCount      = r.ReadUInt16();
        var constantCount       = r.ReadUInt16();
        var samplerCount        = r.ReadUInt16();
        ShaderPackage.Unk = r.ReadUInt32();

        ShaderPackage.ShaderKeys   = r.ReadStructuresAsArray< ShaderKey >( shaderKeyCount );
        ShaderPackage.Constants    = r.ReadStructuresAsArray< Constant >( constantCount );
        ShaderPackage.Samplers     = r.ReadStructuresAsArray< Sampler >( samplerCount );
        ShaderPackage.ShaderValues = r.ReadStructuresAsArray< float >( shaderValueListSize / 4 );
    }

    private static Texture[] ReadTextureOffsets( BinaryReader r, int count, out ushort[] offsets )
    {
        var ret = new Texture[count];
        offsets = new ushort[count];
        for( var i = 0; i < count; ++i )
        {
            offsets[ i ]   = r.ReadUInt16();
            ret[ i ].Flags = r.ReadUInt16();
        }

        return ret;
    }

    private static ColorSet[] ReadColorSetOffsets( BinaryReader r, int count, out ushort[] offsets )
    {
        var ret = new ColorSet[count];
        offsets = new ushort[count];
        for( var i = 0; i < count; ++i )
        {
            offsets[ i ]   = r.ReadUInt16();
            ret[ i ].Index = r.ReadUInt16();
        }

        return ret;
    }

    private static string UseOffset( ReadOnlySpan< byte > strings, ushort offset )
    {
        strings = strings[ offset.. ];
        var end = strings.IndexOf( ( byte )'\0' );
        return Encoding.UTF8.GetString( strings[ ..end ] );
    }
}