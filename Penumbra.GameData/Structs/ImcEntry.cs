using System;
using Newtonsoft.Json;

namespace Penumbra.GameData.Structs;

public readonly struct ImcEntry : IEquatable<ImcEntry>
{
    public          byte   MaterialId { get; init; }
    public          byte   DecalId    { get; init; }
    public readonly ushort AttributeAndSound;
    public          byte   VfxId               { get; init; }
    public          byte   MaterialAnimationId { get; init; }

    public ushort AttributeMask
    {
        get => (ushort)(AttributeAndSound & 0x3FF);
        init => AttributeAndSound = (ushort)((AttributeAndSound & ~0x3FF) | (value & 0x3FF));
    }

    public byte SoundId
    {
        get => (byte)(AttributeAndSound >> 10);
        init => AttributeAndSound = (ushort)(AttributeMask | (value << 10));
    }

    public bool Equals(ImcEntry other)
        => MaterialId == other.MaterialId
         && DecalId == other.DecalId
         && AttributeAndSound == other.AttributeAndSound
         && VfxId == other.VfxId
         && MaterialAnimationId == other.MaterialAnimationId;

    public override bool Equals(object? obj)
        => obj is ImcEntry other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(MaterialId, DecalId, AttributeAndSound, VfxId, MaterialAnimationId);

    [JsonConstructor]
    public ImcEntry(byte materialId, byte decalId, ushort attributeMask, byte soundId, byte vfxId, byte materialAnimationId)
    {
        MaterialId          = materialId;
        DecalId             = decalId;
        AttributeAndSound   = 0;
        VfxId               = vfxId;
        MaterialAnimationId = materialAnimationId;
        AttributeMask       = attributeMask;
        SoundId             = soundId;
    }
}
