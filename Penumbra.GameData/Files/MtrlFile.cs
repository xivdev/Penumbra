using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Lumina.Data.Parsing;
using Lumina.Extensions;
using Penumbra.GameData.Structs;

namespace Penumbra.GameData.Files;

public partial class MtrlFile : IWritable
{
    public struct UvSet
    {
        public string Name;
        public ushort Index;
    }

    public unsafe struct ColorSet
    {
        public struct Row
        {
            public const int Size = 32;

            private fixed ushort _data[16];

            public Vector3 Diffuse
            {
                get => new(ToFloat(0), ToFloat(1), ToFloat(2));
                set
                {
                    _data[0] = FromFloat(value.X);
                    _data[1] = FromFloat(value.Y);
                    _data[2] = FromFloat(value.Z);
                }
            }

            public Vector3 Specular
            {
                get => new(ToFloat(4), ToFloat(5), ToFloat(6));
                set
                {
                    _data[4] = FromFloat(value.X);
                    _data[5] = FromFloat(value.Y);
                    _data[6] = FromFloat(value.Z);
                }
            }

            public Vector3 Emissive
            {
                get => new(ToFloat(8), ToFloat(9), ToFloat(10));
                set
                {
                    _data[8]  = FromFloat(value.X);
                    _data[9]  = FromFloat(value.Y);
                    _data[10] = FromFloat(value.Z);
                }
            }

            public Vector2 MaterialRepeat
            {
                get => new(ToFloat(12), ToFloat(15));
                set
                {
                    _data[12] = FromFloat(value.X);
                    _data[15] = FromFloat(value.Y);
                }
            }

            public Vector2 MaterialSkew
            {
                get => new(ToFloat(13), ToFloat(14));
                set
                {
                    _data[13] = FromFloat(value.X);
                    _data[14] = FromFloat(value.Y);
                }
            }

            public float SpecularStrength
            {
                get => ToFloat(3);
                set => _data[3] = FromFloat(value);
            }

            public float GlossStrength
            {
                get => ToFloat(7);
                set => _data[7] = FromFloat(value);
            }

            public ushort TileSet
            {
                get => (ushort)(ToFloat(11) * 64f);
                set => _data[11] = FromFloat(value / 64f);
            }

            private float ToFloat(int idx)
                => (float)BitConverter.UInt16BitsToHalf(_data[idx]);

            private static ushort FromFloat(float x)
                => BitConverter.HalfToUInt16Bits((Half)x);
        }

        public struct RowArray : IEnumerable<Row>
        {
            public const  int  NumRows = 16;
            private fixed byte _rowData[NumRows * Row.Size];

            public ref Row this[int i]
            {
                get
                {
                    fixed (byte* ptr = _rowData)
                    {
                        return ref ((Row*)ptr)[i];
                    }
                }
            }

            public IEnumerator<Row> GetEnumerator()
            {
                for (var i = 0; i < NumRows; ++i)
                    yield return this[i];
            }

            IEnumerator IEnumerable.GetEnumerator()
                => GetEnumerator();

            public ReadOnlySpan<byte> AsBytes()
            {
                fixed (byte* ptr = _rowData)
                {
                    return new ReadOnlySpan<byte>(ptr, NumRows * Row.Size);
                }
            }
        }

        public RowArray Rows;
        public string   Name;
        public ushort   Index;
        public bool     HasRows;
    }

    public unsafe struct ColorDyeSet
    {
        public struct Row
        {
            private ushort _data;

            public ushort Template
            {
                get => (ushort)(_data >> 5);
                set => _data = (ushort)((_data & 0x1F) | (value << 5));
            }

            public bool Diffuse
            {
                get => (_data & 0x01) != 0;
                set => _data = (ushort)(value ? _data | 0x01 : _data & 0xFFFE);
            }

            public bool Specular
            {
                get => (_data & 0x02) != 0;
                set => _data = (ushort)(value ? _data | 0x02 : _data & 0xFFFD);
            }

            public bool Emissive
            {
                get => (_data & 0x04) != 0;
                set => _data = (ushort)(value ? _data | 0x04 : _data & 0xFFFB);
            }

            public bool Gloss
            {
                get => (_data & 0x08) != 0;
                set => _data = (ushort)(value ? _data | 0x08 : _data & 0xFFF7);
            }

            public bool SpecularStrength
            {
                get => (_data & 0x10) != 0;
                set => _data = (ushort)(value ? _data | 0x10 : _data & 0xFFEF);
            }
        }

        public struct RowArray : IEnumerable<Row>
        {
            public const  int    NumRows = 16;
            private fixed ushort _rowData[NumRows];

            public ref Row this[int i]
            {
                get
                {
                    fixed (ushort* ptr = _rowData)
                    {
                        return ref ((Row*)ptr)[i];
                    }
                }
            }

            public IEnumerator<Row> GetEnumerator()
            {
                for (var i = 0; i < NumRows; ++i)
                    yield return this[i];
            }

            IEnumerator IEnumerable.GetEnumerator()
                => GetEnumerator();

            public ReadOnlySpan<byte> AsBytes()
            {
                fixed (ushort* ptr = _rowData)
                {
                    return new ReadOnlySpan<byte>(ptr, NumRows * sizeof(ushort));
                }
            }
        }

        public RowArray Rows;
        public string   Name;
        public ushort   Index;
    }

    public struct Texture
    {
        public string Path;
        public ushort Flags;

        public bool DX11
            => (Flags & 0x8000) != 0;
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
        public uint        Flags;
    }


    public readonly uint Version;
    public          bool Valid { get; }

    public Texture[]         Textures;
    public UvSet[]           UvSets;
    public ColorSet[]        ColorSets;
    public ColorDyeSet[]     ColorDyeSets;
    public ShaderPackageData ShaderPackage;
    public byte[]            AdditionalData;

    public bool ApplyDyeTemplate(StmFile stm, int colorSetIdx, int rowIdx, StainId stainId)
    {
        if (colorSetIdx < 0 || colorSetIdx >= ColorDyeSets.Length || rowIdx is < 0 or >= ColorSet.RowArray.NumRows)
            return false;

        var dyeSet = ColorDyeSets[colorSetIdx].Rows[rowIdx];
        if (!stm.TryGetValue(dyeSet.Template, stainId, out var dyes))
            return false;

        var ret = false;
        if (dyeSet.Diffuse && ColorSets[colorSetIdx].Rows[rowIdx].Diffuse != dyes.Diffuse)
        {
            ColorSets[colorSetIdx].Rows[rowIdx].Diffuse = dyes.Diffuse;
            ret                                         = true;
        }

        if (dyeSet.Specular && ColorSets[colorSetIdx].Rows[rowIdx].Specular != dyes.Specular)
        {
            ColorSets[colorSetIdx].Rows[rowIdx].Specular = dyes.Specular;
            ret                                          = true;
        }

        if (dyeSet.SpecularStrength && ColorSets[colorSetIdx].Rows[rowIdx].SpecularStrength != dyes.SpecularPower)
        {
            ColorSets[colorSetIdx].Rows[rowIdx].SpecularStrength = dyes.SpecularPower;
            ret                                                  = true;
        }

        if (dyeSet.Emissive && ColorSets[colorSetIdx].Rows[rowIdx].Emissive != dyes.Emissive)
        {
            ColorSets[colorSetIdx].Rows[rowIdx].Emissive = dyes.Emissive;
            ret                                          = true;
        }

        if (dyeSet.Gloss && ColorSets[colorSetIdx].Rows[rowIdx].GlossStrength != dyes.Gloss)
        {
            ColorSets[colorSetIdx].Rows[rowIdx].GlossStrength = dyes.Gloss;
            ret                                               = true;
        }

        return ret;
    }

    public MtrlFile(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var r      = new BinaryReader(stream);

        Version = r.ReadUInt32();
        r.ReadUInt16(); // file size
        var dataSetSize             = r.ReadUInt16();
        var stringTableSize         = r.ReadUInt16();
        var shaderPackageNameOffset = r.ReadUInt16();
        var textureCount            = r.ReadByte();
        var uvSetCount              = r.ReadByte();
        var colorSetCount           = r.ReadByte();
        var additionalDataSize      = r.ReadByte();

        Textures  = ReadTextureOffsets(r, textureCount, out var textureOffsets);
        UvSets    = ReadUvSetOffsets(r, uvSetCount, out var uvOffsets);
        ColorSets = ReadColorSetOffsets(r, colorSetCount, out var colorOffsets);

        var strings = r.ReadBytes(stringTableSize);
        for (var i = 0; i < textureCount; ++i)
            Textures[i].Path = UseOffset(strings, textureOffsets[i]);

        for (var i = 0; i < uvSetCount; ++i)
            UvSets[i].Name = UseOffset(strings, uvOffsets[i]);

        for (var i = 0; i < colorSetCount; ++i)
            ColorSets[i].Name = UseOffset(strings, colorOffsets[i]);

        ColorDyeSets = ColorSets.Length * ColorSet.RowArray.NumRows * ColorSet.Row.Size < dataSetSize
            ? ColorSets.Select(c => new ColorDyeSet
            {
                Index = c.Index,
                Name  = c.Name,
            }).ToArray()
            : Array.Empty<ColorDyeSet>();

        ShaderPackage.Name = UseOffset(strings, shaderPackageNameOffset);

        AdditionalData = r.ReadBytes(additionalDataSize);
        for (var i = 0; i < ColorSets.Length; ++i)
        {
            if (stream.Position + ColorSet.RowArray.NumRows * ColorSet.Row.Size <= stream.Length)
            {
                ColorSets[i].Rows    = r.ReadStructure<ColorSet.RowArray>();
                ColorSets[i].HasRows = true;
            }
            else
            {
                ColorSets[i].HasRows = false;
            }
        }

        for (var i = 0; i < ColorDyeSets.Length; ++i)
            ColorDyeSets[i].Rows = r.ReadStructure<ColorDyeSet.RowArray>();

        var shaderValueListSize = r.ReadUInt16();
        var shaderKeyCount      = r.ReadUInt16();
        var constantCount       = r.ReadUInt16();
        var samplerCount        = r.ReadUInt16();
        ShaderPackage.Flags = r.ReadUInt32();

        ShaderPackage.ShaderKeys   = r.ReadStructuresAsArray<ShaderKey>(shaderKeyCount);
        ShaderPackage.Constants    = r.ReadStructuresAsArray<Constant>(constantCount);
        ShaderPackage.Samplers     = r.ReadStructuresAsArray<Sampler>(samplerCount);
        ShaderPackage.ShaderValues = r.ReadStructuresAsArray<float>(shaderValueListSize / 4);
        Valid                      = true;
    }

    private static Texture[] ReadTextureOffsets(BinaryReader r, int count, out ushort[] offsets)
    {
        var ret = new Texture[count];
        offsets = new ushort[count];
        for (var i = 0; i < count; ++i)
        {
            offsets[i]   = r.ReadUInt16();
            ret[i].Flags = r.ReadUInt16();
        }

        return ret;
    }

    private static UvSet[] ReadUvSetOffsets(BinaryReader r, int count, out ushort[] offsets)
    {
        var ret = new UvSet[count];
        offsets = new ushort[count];
        for (var i = 0; i < count; ++i)
        {
            offsets[i]   = r.ReadUInt16();
            ret[i].Index = r.ReadUInt16();
        }

        return ret;
    }

    private static ColorSet[] ReadColorSetOffsets(BinaryReader r, int count, out ushort[] offsets)
    {
        var ret = new ColorSet[count];
        offsets = new ushort[count];
        for (var i = 0; i < count; ++i)
        {
            offsets[i]   = r.ReadUInt16();
            ret[i].Index = r.ReadUInt16();
        }

        return ret;
    }

    private static string UseOffset(ReadOnlySpan<byte> strings, ushort offset)
    {
        strings = strings[offset..];
        var end = strings.IndexOf((byte)'\0');
        return Encoding.UTF8.GetString(strings[..end]);
    }
}
