using System.IO;
using System.Numerics;
using System.Text;

// ReSharper disable ShiftExpressionZeroLeftOperand

namespace Penumbra.GameData.Files;

public static class AvfxMagic
{
    public const uint AvfxBase                   = ('A' << 24) | ('V' << 16) | ('F' << 8) | (uint)'X';
    public const uint Version                    = (000 << 24) | ('V' << 16) | ('e' << 8) | (uint)'r';
    public const uint IsDelayFastParticle        = ('b' << 24) | ('D' << 16) | ('F' << 8) | (uint)'P';
    public const uint IsFitGround                = (000 << 24) | ('b' << 16) | ('F' << 8) | (uint)'G';
    public const uint IsTransformSkip            = (000 << 24) | ('b' << 16) | ('T' << 8) | (uint)'S';
    public const uint IsAllStopOnHide            = ('b' << 24) | ('A' << 16) | ('S' << 8) | (uint)'H';
    public const uint CanBeClippedOut            = ('b' << 24) | ('C' << 16) | ('B' << 8) | (uint)'C';
    public const uint ClipBoxEnabled             = ('b' << 24) | ('C' << 16) | ('u' << 8) | (uint)'l';
    public const uint ClipBoxX                   = ('C' << 24) | ('B' << 16) | ('P' << 8) | (uint)'x';
    public const uint ClipBoxY                   = ('C' << 24) | ('B' << 16) | ('P' << 8) | (uint)'y';
    public const uint ClipBoxZ                   = ('C' << 24) | ('B' << 16) | ('P' << 8) | (uint)'z';
    public const uint ClipBoxSizeX               = ('C' << 24) | ('B' << 16) | ('S' << 8) | (uint)'x';
    public const uint ClipBoxSizeY               = ('C' << 24) | ('B' << 16) | ('S' << 8) | (uint)'y';
    public const uint ClipBoxSizeZ               = ('C' << 24) | ('B' << 16) | ('S' << 8) | (uint)'z';
    public const uint BiasZmaxScale              = ('Z' << 24) | ('B' << 16) | ('M' << 8) | (uint)'s';
    public const uint BiasZmaxDistance           = ('Z' << 24) | ('B' << 16) | ('M' << 8) | (uint)'d';
    public const uint IsCameraSpace              = ('b' << 24) | ('C' << 16) | ('m' << 8) | (uint)'S';
    public const uint IsFullEnvLight             = ('b' << 24) | ('F' << 16) | ('E' << 8) | (uint)'L';
    public const uint IsClipOwnSetting           = ('b' << 24) | ('O' << 16) | ('S' << 8) | (uint)'t';
    public const uint NearClipBegin              = (000 << 24) | ('N' << 16) | ('C' << 8) | (uint)'B';
    public const uint NearClipEnd                = (000 << 24) | ('N' << 16) | ('C' << 8) | (uint)'E';
    public const uint FarClipBegin               = (000 << 24) | ('F' << 16) | ('C' << 8) | (uint)'B';
    public const uint FarClipEnd                 = (000 << 24) | ('F' << 16) | ('C' << 8) | (uint)'E';
    public const uint SoftParticleFadeRange      = ('S' << 24) | ('P' << 16) | ('F' << 8) | (uint)'R';
    public const uint SoftKeyOffset              = (000 << 24) | ('S' << 16) | ('K' << 8) | (uint)'O';
    public const uint DrawLayerType              = ('D' << 24) | ('w' << 16) | ('L' << 8) | (uint)'y';
    public const uint DrawOrderType              = ('D' << 24) | ('w' << 16) | ('O' << 8) | (uint)'T';
    public const uint DirectionalLightSourceType = ('D' << 24) | ('L' << 16) | ('S' << 8) | (uint)'T';
    public const uint PointLightsType1           = ('P' << 24) | ('L' << 16) | ('1' << 8) | (uint)'S';
    public const uint PointLightsType2           = ('P' << 24) | ('L' << 16) | ('2' << 8) | (uint)'S';
    public const uint RevisedValuesPosX          = ('R' << 24) | ('v' << 16) | ('P' << 8) | (uint)'x';
    public const uint RevisedValuesPosY          = ('R' << 24) | ('v' << 16) | ('P' << 8) | (uint)'y';
    public const uint RevisedValuesPosZ          = ('R' << 24) | ('v' << 16) | ('P' << 8) | (uint)'z';
    public const uint RevisedValuesRotX          = ('R' << 24) | ('v' << 16) | ('R' << 8) | (uint)'x';
    public const uint RevisedValuesRotY          = ('R' << 24) | ('v' << 16) | ('R' << 8) | (uint)'y';
    public const uint RevisedValuesRotZ          = ('R' << 24) | ('v' << 16) | ('R' << 8) | (uint)'z';
    public const uint RevisedValuesScaleX        = ('R' << 24) | ('v' << 16) | ('S' << 8) | (uint)'x';
    public const uint RevisedValuesScaleY        = ('R' << 24) | ('v' << 16) | ('S' << 8) | (uint)'y';
    public const uint RevisedValuesScaleZ        = ('R' << 24) | ('v' << 16) | ('S' << 8) | (uint)'z';
    public const uint RevisedValuesColorR        = (000 << 24) | ('R' << 16) | ('v' << 8) | (uint)'R';
    public const uint RevisedValuesColorG        = (000 << 24) | ('R' << 16) | ('v' << 8) | (uint)'G';
    public const uint RevisedValuesColorB        = (000 << 24) | ('R' << 16) | ('v' << 8) | (uint)'B';
    public const uint FadeEnabledX               = ('A' << 24) | ('F' << 16) | ('X' << 8) | (uint)'e';
    public const uint FadeInnerX                 = ('A' << 24) | ('F' << 16) | ('X' << 8) | (uint)'i';
    public const uint FadeOuterX                 = ('A' << 24) | ('F' << 16) | ('X' << 8) | (uint)'o';
    public const uint FadeEnabledY               = ('A' << 24) | ('F' << 16) | ('Y' << 8) | (uint)'e';
    public const uint FadeInnerY                 = ('A' << 24) | ('F' << 16) | ('Y' << 8) | (uint)'i';
    public const uint FadeOuterY                 = ('A' << 24) | ('F' << 16) | ('Y' << 8) | (uint)'o';
    public const uint FadeEnabledZ               = ('A' << 24) | ('F' << 16) | ('Z' << 8) | (uint)'e';
    public const uint FadeInnerZ                 = ('A' << 24) | ('F' << 16) | ('Z' << 8) | (uint)'i';
    public const uint FadeOuterZ                 = ('A' << 24) | ('F' << 16) | ('Z' << 8) | (uint)'o';
    public const uint GlobalFogEnabled           = ('b' << 24) | ('G' << 16) | ('F' << 8) | (uint)'E';
    public const uint GlobalFogInfluence         = ('G' << 24) | ('F' << 16) | ('I' << 8) | (uint)'M';
    public const uint LtsEnabled                 = ('b' << 24) | ('L' << 16) | ('T' << 8) | (uint)'S';
    public const uint NumSchedulers              = ('S' << 24) | ('c' << 16) | ('C' << 8) | (uint)'n';
    public const uint NumTimelines               = ('T' << 24) | ('l' << 16) | ('C' << 8) | (uint)'n';
    public const uint NumEmitters                = ('E' << 24) | ('m' << 16) | ('C' << 8) | (uint)'n';
    public const uint NumParticles               = ('P' << 24) | ('r' << 16) | ('C' << 8) | (uint)'n';
    public const uint NumEffectors               = ('E' << 24) | ('f' << 16) | ('C' << 8) | (uint)'n';
    public const uint NumBinders                 = ('B' << 24) | ('d' << 16) | ('C' << 8) | (uint)'n';
    public const uint NumTextures                = ('T' << 24) | ('x' << 16) | ('C' << 8) | (uint)'n';
    public const uint NumModels                  = ('M' << 24) | ('d' << 16) | ('C' << 8) | (uint)'n';
    public const uint Scheduler                  = ('S' << 24) | ('c' << 16) | ('h' << 8) | (uint)'d';
    public const uint Timeline                   = ('T' << 24) | ('m' << 16) | ('L' << 8) | (uint)'n';
    public const uint Emitter                    = ('E' << 24) | ('m' << 16) | ('i' << 8) | (uint)'t';
    public const uint Particle                   = ('P' << 24) | ('t' << 16) | ('c' << 8) | (uint)'l';
    public const uint Effector                   = ('E' << 24) | ('f' << 16) | ('c' << 8) | (uint)'t';
    public const uint Binder                     = ('B' << 24) | ('i' << 16) | ('n' << 8) | (uint)'d';
    public const uint Texture                    = (000 << 24) | ('T' << 16) | ('e' << 8) | (uint)'x';
    public const uint Model                      = ('M' << 24) | ('o' << 16) | ('d' << 8) | (uint)'l';

    internal static uint RoundTo4(this uint size)
    {
        var rest = size & 0b11u;
        return rest > 0 ? (size & ~0b11u) + 4u : size;
    }

    internal static BinaryWriter WriteTextureBlock(this BinaryWriter bw, string texture)
    {
        bw.Write(Texture);
        var bytes = Encoding.UTF8.GetBytes(texture);
        var size  = (uint)bytes.Length + 1u;
        bw.Write(size);
        bw.Write(bytes);
        bw.Write((byte)0);
        for (var end = size.RoundTo4(); size < end; ++size)
            bw.Write((byte)0);
        return bw;
    }

    internal static BinaryWriter WriteBlock(this BinaryWriter bw, AvfxFile.Block block)
    {
        bw.Write(block.Name);
        bw.Write(block.Size);
        bw.Write(block.Data);
        return bw;
    }

    internal static BinaryWriter WriteBlock(this BinaryWriter bw, uint magic, uint value)
    {
        if (value != uint.MaxValue)
        {
            bw.Write(magic);
            bw.Write(4u);
            bw.Write(value);
        }

        return bw;
    }

    internal static BinaryWriter WriteBlock(this BinaryWriter bw, uint magic, byte value)
    {
        if (value != byte.MaxValue)
        {
            bw.Write(magic);
            bw.Write(4u);
            bw.Write(value == 1 ? 1u : 0u);
        }

        return bw;
    }

    internal static BinaryWriter WriteBlock(this BinaryWriter bw, uint magic, float value)
    {
        if (!float.IsNaN(value))
        {
            bw.Write(magic);
            bw.Write(4u);
            bw.Write(value);
        }

        return bw;
    }

    internal static BinaryWriter WriteBlock(this BinaryWriter bw, uint magicX, uint magicY, uint magicZ, Vector3 value)
        => bw.WriteBlock(magicX, value.X)
            .WriteBlock(magicY, value.Y)
            .WriteBlock(magicZ, value.Z);
}
