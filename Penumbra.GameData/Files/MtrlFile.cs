using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lumina.Data.Parsing;
using Lumina.Extensions;
using Penumbra.GameData.Structs;

namespace Penumbra.GameData.Files;

public partial class MtrlFile : IWritable
{
    public readonly uint Version;

    public bool Valid
        => CheckTextures();

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

    public Span<float> GetConstantValues(Constant constant)
    {
        if ((constant.ByteOffset & 0x3) != 0
         || (constant.ByteSize & 0x3) != 0
         || (constant.ByteOffset + constant.ByteSize) >> 2 > ShaderPackage.ShaderValues.Length)
            return null;

        return ShaderPackage.ShaderValues.AsSpan().Slice(constant.ByteOffset >> 2, constant.ByteSize >> 2);

    }

    public List<(Sampler?, ShpkFile.Resource?)> GetSamplersByTexture(ShpkFile? shpk)
    {
        var samplers = new List<(Sampler?, ShpkFile.Resource?)>();
        for (var i = 0; i < Textures.Length; ++i)
        {
            samplers.Add((null, null));
        }
        foreach (var sampler in ShaderPackage.Samplers)
        {
            samplers[sampler.TextureIndex] = (sampler, shpk?.GetSamplerById(sampler.SamplerId));
        }

        return samplers;
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
        return Encoding.UTF8.GetString(end == -1 ? strings : strings[..end]);
    }

    private bool CheckTextures()
        => Textures.All(texture => texture.Path.Contains('/'));

    public struct UvSet
    {
        public string Name;
        public ushort Index;
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
        public uint   Id;
        public ushort ByteOffset;
        public ushort ByteSize;
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
}
