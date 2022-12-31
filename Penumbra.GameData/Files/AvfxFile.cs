using System;
using System.IO;
using System.Numerics;
using System.Text;

namespace Penumbra.GameData.Files;

public class AvfxFile : IWritable
{
    public struct Block
    {
        public uint   Name;
        public uint   Size;
        public byte[] Data;

        public Block(BinaryReader r)
        {
            Name = r.ReadUInt32();
            Size = r.ReadUInt32();
            Data = r.ReadBytes((int)Size.RoundTo4());
        }

        public byte ToBool()
            => BitConverter.ToBoolean(Data) ? (byte)1 : (byte)0;

        public uint ToUint()
            => BitConverter.ToUInt32(Data);

        public float ToFloat()
            => BitConverter.ToSingle(Data);

        public new string ToString()
        {
            var span = Data.AsSpan(0, (int)Size - 1);
            return Encoding.UTF8.GetString(span);
        }
    }

    public static readonly Vector3 BadVector = new(float.NaN);

    public Vector3 ClipBox            = BadVector;
    public Vector3 ClipBoxSize        = BadVector;
    public Vector3 RevisedValuesPos   = BadVector;
    public Vector3 RevisedValuesRot   = BadVector;
    public Vector3 RevisedValuesScale = BadVector;
    public Vector3 RevisedValuesColor = BadVector;

    public uint Version                    = uint.MaxValue;
    public uint DrawLayerType              = uint.MaxValue;
    public uint DrawOrderType              = uint.MaxValue;
    public uint DirectionalLightSourceType = uint.MaxValue;
    public uint PointLightsType1           = uint.MaxValue;
    public uint PointLightsType2           = uint.MaxValue;

    public float BiasZmaxScale         = float.NaN;
    public float BiasZmaxDistance      = float.NaN;
    public float NearClipBegin         = float.NaN;
    public float NearClipEnd           = float.NaN;
    public float FadeInnerX            = float.NaN;
    public float FadeOuterX            = float.NaN;
    public float FadeInnerY            = float.NaN;
    public float FadeOuterY            = float.NaN;
    public float FadeInnerZ            = float.NaN;
    public float FadeOuterZ            = float.NaN;
    public float FarClipBegin          = float.NaN;
    public float FarClipEnd            = float.NaN;
    public float SoftParticleFadeRange = float.NaN;
    public float SoftKeyOffset         = float.NaN;
    public float GlobalFogInfluence    = float.NaN;

    public byte IsDelayFastParticle = byte.MaxValue;
    public byte IsFitGround         = byte.MaxValue;
    public byte IsTransformSkip     = byte.MaxValue;
    public byte IsAllStopOnHide     = byte.MaxValue;
    public byte CanBeClippedOut     = byte.MaxValue;
    public byte ClipBoxEnabled      = byte.MaxValue;
    public byte IsCameraSpace       = byte.MaxValue;
    public byte IsFullEnvLight      = byte.MaxValue;
    public byte IsClipOwnSetting    = byte.MaxValue;
    public byte FadeEnabledX        = byte.MaxValue;
    public byte FadeEnabledY        = byte.MaxValue;
    public byte FadeEnabledZ        = byte.MaxValue;
    public byte GlobalFogEnabled    = byte.MaxValue;
    public byte LtsEnabled          = byte.MaxValue;

    public Block[]  Schedulers = Array.Empty<Block>();
    public Block[]  Timelines  = Array.Empty<Block>();
    public Block[]  Emitters   = Array.Empty<Block>();
    public Block[]  Particles  = Array.Empty<Block>();
    public Block[]  Effectors  = Array.Empty<Block>();
    public Block[]  Binders    = Array.Empty<Block>();
    public string[] Textures   = Array.Empty<string>();
    public Block[]  Models     = Array.Empty<Block>();

    public bool Valid
        => true;

    public AvfxFile(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var r      = new BinaryReader(stream);

        var name           = r.ReadUInt32();
        var size           = r.ReadUInt32();
        var schedulerCount = 0;
        var timelineCount  = 0;
        var emitterCount   = 0;
        var particleCount  = 0;
        var effectorCount  = 0;
        var binderCount    = 0;
        var textureCount   = 0;
        var modelCount     = 0;
        while (r.BaseStream.Position < size)
        {
            var block = new Block(r);
            switch (block.Name)
            {
                // @formatter:off
                case AvfxMagic.Version:                    Version                      = block.ToUint();             break;
                case AvfxMagic.IsDelayFastParticle:        IsDelayFastParticle          = block.ToBool();             break;
                case AvfxMagic.IsFitGround:                IsFitGround                  = block.ToBool();             break;
                case AvfxMagic.IsTransformSkip:            IsTransformSkip              = block.ToBool();             break;
                case AvfxMagic.IsAllStopOnHide:            IsAllStopOnHide              = block.ToBool();             break;
                case AvfxMagic.CanBeClippedOut:            CanBeClippedOut              = block.ToBool();             break;
                case AvfxMagic.ClipBoxEnabled:             ClipBoxEnabled               = block.ToBool();             break;
                case AvfxMagic.ClipBoxX:                   ClipBox.X                    = block.ToFloat();            break;
                case AvfxMagic.ClipBoxY:                   ClipBox.Y                    = block.ToFloat();            break;
                case AvfxMagic.ClipBoxZ:                   ClipBox.Z                    = block.ToFloat();            break;
                case AvfxMagic.ClipBoxSizeX:               ClipBoxSize.X                = block.ToFloat();            break;
                case AvfxMagic.ClipBoxSizeY:               ClipBoxSize.Y                = block.ToFloat();            break;
                case AvfxMagic.ClipBoxSizeZ:               ClipBoxSize.Z                = block.ToFloat();            break;
                case AvfxMagic.BiasZmaxScale:              BiasZmaxScale                = block.ToFloat();            break;
                case AvfxMagic.BiasZmaxDistance:           BiasZmaxDistance             = block.ToFloat();            break;
                case AvfxMagic.IsCameraSpace:              IsCameraSpace                = block.ToBool();             break;
                case AvfxMagic.IsFullEnvLight:             IsFullEnvLight               = block.ToBool();             break;
                case AvfxMagic.IsClipOwnSetting:           IsClipOwnSetting             = block.ToBool();             break;
                case AvfxMagic.NearClipBegin:              NearClipBegin                = block.ToFloat();            break;
                case AvfxMagic.NearClipEnd:                NearClipEnd                  = block.ToFloat();            break;
                case AvfxMagic.FarClipBegin:               FarClipBegin                 = block.ToFloat();            break;
                case AvfxMagic.FarClipEnd:                 FarClipEnd                   = block.ToFloat();            break;
                case AvfxMagic.SoftParticleFadeRange:      SoftParticleFadeRange        = block.ToFloat();            break;
                case AvfxMagic.SoftKeyOffset:              SoftKeyOffset                = block.ToFloat();            break;
                case AvfxMagic.DrawLayerType:              DrawLayerType                = block.ToUint();             break;
                case AvfxMagic.DrawOrderType:              DrawOrderType                = block.ToUint();             break;
                case AvfxMagic.DirectionalLightSourceType: DirectionalLightSourceType   = block.ToUint();             break;
                case AvfxMagic.PointLightsType1:           PointLightsType1             = block.ToUint();             break;
                case AvfxMagic.PointLightsType2:           PointLightsType2             = block.ToUint();             break;
                case AvfxMagic.RevisedValuesPosX:          RevisedValuesPos.X           = block.ToFloat();            break;
                case AvfxMagic.RevisedValuesPosY:          RevisedValuesPos.Y           = block.ToFloat();            break;
                case AvfxMagic.RevisedValuesPosZ:          RevisedValuesPos.Z           = block.ToFloat();            break;
                case AvfxMagic.RevisedValuesRotX:          RevisedValuesRot.X           = block.ToFloat();            break;
                case AvfxMagic.RevisedValuesRotY:          RevisedValuesRot.Y           = block.ToFloat();            break;
                case AvfxMagic.RevisedValuesRotZ:          RevisedValuesRot.Z           = block.ToFloat();            break;
                case AvfxMagic.RevisedValuesScaleX:        RevisedValuesScale.X         = block.ToFloat();            break;
                case AvfxMagic.RevisedValuesScaleY:        RevisedValuesScale.Y         = block.ToFloat();            break;
                case AvfxMagic.RevisedValuesScaleZ:        RevisedValuesScale.Z         = block.ToFloat();            break;
                case AvfxMagic.RevisedValuesColorR:        RevisedValuesColor.X         = block.ToFloat();            break;
                case AvfxMagic.RevisedValuesColorG:        RevisedValuesColor.Y         = block.ToFloat();            break;
                case AvfxMagic.RevisedValuesColorB:        RevisedValuesColor.Z         = block.ToFloat();            break;
                case AvfxMagic.FadeEnabledX:               FadeEnabledX                 = block.ToBool();             break;
                case AvfxMagic.FadeInnerX:                 FadeInnerX                   = block.ToFloat();            break;
                case AvfxMagic.FadeOuterX:                 FadeOuterX                   = block.ToFloat();            break;
                case AvfxMagic.FadeEnabledY:               FadeEnabledY                 = block.ToBool();             break;
                case AvfxMagic.FadeInnerY:                 FadeInnerY                   = block.ToFloat();            break;
                case AvfxMagic.FadeOuterY:                 FadeOuterY                   = block.ToFloat();            break;
                case AvfxMagic.FadeEnabledZ:               FadeEnabledZ                 = block.ToBool();             break;
                case AvfxMagic.FadeInnerZ:                 FadeInnerZ                   = block.ToFloat();            break;
                case AvfxMagic.FadeOuterZ:                 FadeOuterZ                   = block.ToFloat();            break;
                case AvfxMagic.GlobalFogEnabled:           GlobalFogEnabled             = block.ToBool();             break;
                case AvfxMagic.GlobalFogInfluence:         GlobalFogInfluence           = block.ToFloat();            break;
                case AvfxMagic.LtsEnabled:                 LtsEnabled                   = block.ToBool();             break;
                case AvfxMagic.NumSchedulers:              Schedulers                   = new Block[block.ToUint()];  break;
                case AvfxMagic.NumTimelines:               Timelines                    = new Block[block.ToUint()];  break;
                case AvfxMagic.NumEmitters:                Emitters                     = new Block[block.ToUint()];  break;
                case AvfxMagic.NumParticles:               Particles                    = new Block[block.ToUint()];  break;
                case AvfxMagic.NumEffectors:               Effectors                    = new Block[block.ToUint()];  break;
                case AvfxMagic.NumBinders:                 Binders                      = new Block[block.ToUint()];  break;
                case AvfxMagic.NumTextures:                Textures                     = new string[block.ToUint()]; break;
                case AvfxMagic.NumModels:                  Models                       = new Block[block.ToUint()];  break;
                case AvfxMagic.Scheduler:                  Schedulers[schedulerCount++] = block;                      break;
                case AvfxMagic.Timeline:                   Timelines[timelineCount++]   = block;                      break;
                case AvfxMagic.Emitter:                    Emitters[emitterCount++]     = block;                      break;
                case AvfxMagic.Particle:                   Particles[particleCount++]   = block;                      break;
                case AvfxMagic.Effector:                   Effectors[effectorCount++]   = block;                      break;
                case AvfxMagic.Binder:                     Binders[binderCount++]       = block;                      break;
                case AvfxMagic.Texture:                    Textures[textureCount++]     = block.ToString();           break;
                case AvfxMagic.Model:                      Models[modelCount++]         = block;                      break;
                // @formatter:on
            }
        }
    }


    public byte[] Write()
    {
        using var m = new MemoryStream(512 * 1024);
        using var w = new BinaryWriter(m);

        w.Write(AvfxMagic.AvfxBase);
        var sizePos = w.BaseStream.Position;
        w.Write(0u);
        w.WriteBlock(AvfxMagic.Version, Version)
            .WriteBlock(AvfxMagic.IsDelayFastParticle,        IsDelayFastParticle)
            .WriteBlock(AvfxMagic.IsFitGround,                IsFitGround)
            .WriteBlock(AvfxMagic.IsTransformSkip,            IsTransformSkip)
            .WriteBlock(AvfxMagic.IsAllStopOnHide,            IsAllStopOnHide)
            .WriteBlock(AvfxMagic.CanBeClippedOut,            CanBeClippedOut)
            .WriteBlock(AvfxMagic.ClipBoxEnabled,             ClipBoxEnabled)
            .WriteBlock(AvfxMagic.ClipBoxX,                   ClipBox.X)
            .WriteBlock(AvfxMagic.ClipBoxY,                   ClipBox.Y)
            .WriteBlock(AvfxMagic.ClipBoxZ,                   ClipBox.Z)
            .WriteBlock(AvfxMagic.ClipBoxSizeX,               ClipBoxSize.X)
            .WriteBlock(AvfxMagic.ClipBoxSizeY,               ClipBoxSize.Y)
            .WriteBlock(AvfxMagic.ClipBoxSizeZ,               ClipBoxSize.Z)
            .WriteBlock(AvfxMagic.BiasZmaxScale,              BiasZmaxScale)
            .WriteBlock(AvfxMagic.BiasZmaxDistance,           BiasZmaxDistance)
            .WriteBlock(AvfxMagic.IsCameraSpace,              IsCameraSpace)
            .WriteBlock(AvfxMagic.IsFullEnvLight,             IsFullEnvLight)
            .WriteBlock(AvfxMagic.IsClipOwnSetting,           IsClipOwnSetting)
            .WriteBlock(AvfxMagic.NearClipBegin,              NearClipBegin)
            .WriteBlock(AvfxMagic.NearClipEnd,                NearClipEnd)
            .WriteBlock(AvfxMagic.FarClipBegin,               FarClipBegin)
            .WriteBlock(AvfxMagic.FarClipEnd,                 FarClipEnd)
            .WriteBlock(AvfxMagic.SoftParticleFadeRange,      SoftParticleFadeRange)
            .WriteBlock(AvfxMagic.SoftKeyOffset,              SoftKeyOffset)
            .WriteBlock(AvfxMagic.DrawLayerType,              DrawLayerType)
            .WriteBlock(AvfxMagic.DrawOrderType,              DrawOrderType)
            .WriteBlock(AvfxMagic.DirectionalLightSourceType, DirectionalLightSourceType)
            .WriteBlock(AvfxMagic.PointLightsType1,           PointLightsType1)
            .WriteBlock(AvfxMagic.PointLightsType2,           PointLightsType2)
            .WriteBlock(AvfxMagic.RevisedValuesPosX,          RevisedValuesPos.X)
            .WriteBlock(AvfxMagic.RevisedValuesPosY,          RevisedValuesPos.Y)
            .WriteBlock(AvfxMagic.RevisedValuesPosZ,          RevisedValuesPos.Z)
            .WriteBlock(AvfxMagic.RevisedValuesRotX,          RevisedValuesRot.X)
            .WriteBlock(AvfxMagic.RevisedValuesRotY,          RevisedValuesRot.Y)
            .WriteBlock(AvfxMagic.RevisedValuesRotZ,          RevisedValuesRot.Z)
            .WriteBlock(AvfxMagic.RevisedValuesScaleX,        RevisedValuesScale.X)
            .WriteBlock(AvfxMagic.RevisedValuesScaleY,        RevisedValuesScale.Y)
            .WriteBlock(AvfxMagic.RevisedValuesScaleZ,        RevisedValuesScale.Z)
            .WriteBlock(AvfxMagic.RevisedValuesColorR,        RevisedValuesColor.X)
            .WriteBlock(AvfxMagic.RevisedValuesColorG,        RevisedValuesColor.Y)
            .WriteBlock(AvfxMagic.RevisedValuesColorB,        RevisedValuesColor.Z)
            .WriteBlock(AvfxMagic.FadeEnabledX,               FadeEnabledX)
            .WriteBlock(AvfxMagic.FadeInnerX,                 FadeInnerX)
            .WriteBlock(AvfxMagic.FadeOuterX,                 FadeOuterX)
            .WriteBlock(AvfxMagic.FadeEnabledY,               FadeEnabledY)
            .WriteBlock(AvfxMagic.FadeInnerY,                 FadeInnerY)
            .WriteBlock(AvfxMagic.FadeOuterY,                 FadeOuterY)
            .WriteBlock(AvfxMagic.FadeEnabledZ,               FadeEnabledZ)
            .WriteBlock(AvfxMagic.FadeInnerZ,                 FadeInnerZ)
            .WriteBlock(AvfxMagic.FadeOuterZ,                 FadeOuterZ)
            .WriteBlock(AvfxMagic.GlobalFogEnabled,           GlobalFogEnabled)
            .WriteBlock(AvfxMagic.GlobalFogInfluence,         GlobalFogInfluence)
            .WriteBlock(AvfxMagic.LtsEnabled,                 LtsEnabled)
            .WriteBlock(AvfxMagic.NumSchedulers,              (uint)Schedulers.Length)
            .WriteBlock(AvfxMagic.NumTimelines,               (uint)Timelines.Length)
            .WriteBlock(AvfxMagic.NumEmitters,                (uint)Emitters.Length)
            .WriteBlock(AvfxMagic.NumParticles,               (uint)Particles.Length)
            .WriteBlock(AvfxMagic.NumEffectors,               (uint)Effectors.Length)
            .WriteBlock(AvfxMagic.NumBinders,                 (uint)Binders.Length)
            .WriteBlock(AvfxMagic.NumTextures,                (uint)Textures.Length)
            .WriteBlock(AvfxMagic.NumModels,                  (uint)Models.Length);
        foreach (var block in Schedulers)
            w.WriteBlock(block);
        foreach (var block in Timelines)
            w.WriteBlock(block);
        foreach (var block in Emitters)
            w.WriteBlock(block);
        foreach (var block in Particles)
            w.WriteBlock(block);
        foreach (var block in Effectors)
            w.WriteBlock(block);
        foreach (var block in Binders)
            w.WriteBlock(block);
        foreach (var texture in Textures)
            w.WriteTextureBlock(texture);
        foreach (var block in Models)
            w.WriteBlock(block);
        w.Seek((int)sizePos, SeekOrigin.Begin);
        w.Write((uint)w.BaseStream.Length - 8u);
        return m.ToArray();
    }
}
