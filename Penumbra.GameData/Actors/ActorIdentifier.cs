using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Objects.Enums;
using Newtonsoft.Json.Linq;
using Penumbra.String;

namespace Penumbra.GameData.Actors;

[StructLayout(LayoutKind.Explicit)]
public readonly struct ActorIdentifier : IEquatable<ActorIdentifier>
{
    public static ActorManager? Manager;

    public static readonly ActorIdentifier Invalid = new(IdentifierType.Invalid, 0, 0, 0, ByteString.Empty);

    public enum RetainerType : ushort
    {
        Both = 0,
        Bell = 1,
        Mannequin = 2,
    }

    // @formatter:off
    [FieldOffset( 0 )] public readonly IdentifierType Type;       // All
    [FieldOffset( 1 )] public readonly ObjectKind     Kind;       // Npc, Owned
    [FieldOffset( 2 )] public readonly ushort         HomeWorld;  // Player, Owned
    [FieldOffset( 2 )] public readonly ushort         Index;      // NPC
    [FieldOffset( 2 )] public readonly RetainerType   Retainer;   // Retainer
    [FieldOffset( 2 )] public readonly ScreenActor    Special;    // Special
    [FieldOffset( 4 )] public readonly uint           DataId;     // Owned, NPC
    [FieldOffset( 8 )] public readonly ByteString     PlayerName; // Player, Owned
    // @formatter:on

    public ActorIdentifier CreatePermanent()
        => new(Type, Kind, Index, DataId, PlayerName.IsEmpty || PlayerName.IsOwned ? PlayerName : PlayerName.Clone());

    public bool Equals(ActorIdentifier other)
    {
        if (Type != other.Type)
            return false;

        return Type switch
        {
            IdentifierType.Player => HomeWorld == other.HomeWorld && PlayerName.EqualsCi(other.PlayerName),
            IdentifierType.Retainer => PlayerName.EqualsCi(other.PlayerName),
            IdentifierType.Owned => HomeWorld == other.HomeWorld && PlayerName.EqualsCi(other.PlayerName) && Manager.DataIdEquals(this, other),
            IdentifierType.Special => Special == other.Special,
            IdentifierType.Npc => Manager.DataIdEquals(this, other)
             && (Index == other.Index || Index == ushort.MaxValue || other.Index == ushort.MaxValue),
            IdentifierType.UnkObject => PlayerName.EqualsCi(other.PlayerName) && Index == other.Index,
            _                        => false,
        };
    }

    public override bool Equals(object? obj)
        => obj is ActorIdentifier other && Equals(other);

    public static bool operator ==(ActorIdentifier lhs, ActorIdentifier rhs)
        => lhs.Equals(rhs);

    public static bool operator !=(ActorIdentifier lhs, ActorIdentifier rhs)
        => !lhs.Equals(rhs);

    public bool IsValid
        => Type is not (IdentifierType.UnkObject or IdentifierType.Invalid);

    public string Incognito(string? name)
    {
        name ??= ToString();
        if (Type is not (IdentifierType.Player or IdentifierType.Owned))
            return name;

        var parts = name.Split(' ', 3);
        return string.Join(" ",
            parts.Length != 3 ? parts.Select(n => $"{n[0]}.") : parts[..2].Select(n => $"{n[0]}.").Append(parts[2]));
    }

    public override string ToString()
        => Manager?.ToString(this)
         ?? Type switch
            {
                IdentifierType.Player   => $"{PlayerName} ({HomeWorld})",
                IdentifierType.Retainer => $"{PlayerName} (Retainer)",
                IdentifierType.Owned    => $"{PlayerName}s {Kind.ToName()} {DataId} ({HomeWorld})",
                IdentifierType.Special  => Special.ToName(),
                IdentifierType.Npc =>
                    Index == ushort.MaxValue
                        ? $"{Kind.ToName()} #{DataId}"
                        : $"{Kind.ToName()} #{DataId} at {Index}",
                IdentifierType.UnkObject => PlayerName.IsEmpty
                    ? $"Unknown Object at {Index}"
                    : $"{PlayerName} at {Index}",
                _ => "Invalid",
            };

    public override int GetHashCode()
        => Type switch
        {
            IdentifierType.Player    => HashCode.Combine(IdentifierType.Player,    PlayerName, HomeWorld),
            IdentifierType.Retainer  => HashCode.Combine(IdentifierType.Player,    PlayerName),
            IdentifierType.Owned     => HashCode.Combine(IdentifierType.Owned,     Kind, PlayerName, HomeWorld, DataId),
            IdentifierType.Special   => HashCode.Combine(IdentifierType.Special,   Special),
            IdentifierType.Npc       => HashCode.Combine(IdentifierType.Npc,       Kind,       DataId),
            IdentifierType.UnkObject => HashCode.Combine(IdentifierType.UnkObject, PlayerName, Index),
            _                        => 0,
        };

    internal ActorIdentifier(IdentifierType type, ObjectKind kind, ushort index, uint data, ByteString playerName)
    {
        Type       = type;
        Kind       = kind;
        Special    = (ScreenActor)index;
        HomeWorld  = Index = index;
        DataId     = data;
        PlayerName = playerName;
    }

    public JObject ToJson()
    {
        var ret = new JObject { { nameof(Type), Type.ToString() } };
        switch (Type)
        {
            case IdentifierType.Player:
                ret.Add(nameof(PlayerName), PlayerName.ToString());
                ret.Add(nameof(HomeWorld),  HomeWorld);
                return ret;
            case IdentifierType.Retainer:
                ret.Add(nameof(PlayerName), PlayerName.ToString());
                return ret;
            case IdentifierType.Owned:
                ret.Add(nameof(PlayerName), PlayerName.ToString());
                ret.Add(nameof(HomeWorld),  HomeWorld);
                ret.Add(nameof(Kind),       Kind.ToString());
                ret.Add(nameof(DataId),     DataId);
                return ret;
            case IdentifierType.Special:
                ret.Add(nameof(Special), Special.ToString());
                return ret;
            case IdentifierType.Npc:
                ret.Add(nameof(Kind), Kind.ToString());
                if (Index != ushort.MaxValue)
                    ret.Add(nameof(Index), Index);
                ret.Add(nameof(DataId), DataId);
                return ret;
            case IdentifierType.UnkObject:
                ret.Add(nameof(PlayerName), PlayerName.ToString());
                ret.Add(nameof(Index),      Index);
                return ret;
        }

        return ret;
    }
}

public static class ActorManagerExtensions
{
    public static bool DataIdEquals(this ActorManager? manager, ActorIdentifier lhs, ActorIdentifier rhs)
    {
        if (lhs.Kind != rhs.Kind)
            return false;

        if (lhs.DataId == rhs.DataId)
            return true;

        if (manager == null)
            return lhs.Kind == rhs.Kind && lhs.DataId == rhs.DataId || lhs.DataId == uint.MaxValue || rhs.DataId == uint.MaxValue;

        var dict = lhs.Kind switch
        {
            ObjectKind.MountType => manager.Data.Mounts,
            ObjectKind.Companion => manager.Data.Companions,
            ObjectKind.Ornament  => manager.Data.Ornaments,
            ObjectKind.BattleNpc => manager.Data.BNpcs,
            ObjectKind.EventNpc  => manager.Data.ENpcs,
            _                    => new Dictionary<uint, string>(),
        };

        return dict.TryGetValue(lhs.DataId, out var lhsName)
         && dict.TryGetValue(rhs.DataId,    out var rhsName)
         && lhsName.Equals(rhsName, StringComparison.OrdinalIgnoreCase);
    }

    public static string ToName(this ObjectKind kind)
        => kind switch
        {
            ObjectKind.None      => "Unknown",
            ObjectKind.BattleNpc => "Battle NPC",
            ObjectKind.EventNpc  => "Event NPC",
            ObjectKind.MountType => "Mount",
            ObjectKind.Companion => "Companion",
            ObjectKind.Ornament  => "Accessory",
            _                    => kind.ToString(),
        };

    public static string ToName(this IdentifierType type)
        => type switch
        {
            IdentifierType.Player    => "Player",
            IdentifierType.Retainer  => "Retainer (Bell)",
            IdentifierType.Owned     => "Owned NPC",
            IdentifierType.Special   => "Special Actor",
            IdentifierType.Npc       => "NPC",
            IdentifierType.UnkObject => "Unknown Object",
            _                        => "Invalid",
        };

    /// <summary>
    /// Fixed names for special actors.
    /// </summary>
    public static string ToName(this ScreenActor actor)
        => actor switch
        {
            ScreenActor.CharacterScreen => "Character Screen Actor",
            ScreenActor.ExamineScreen   => "Examine Screen Actor",
            ScreenActor.FittingRoom     => "Fitting Room Actor",
            ScreenActor.DyePreview      => "Dye Preview Actor",
            ScreenActor.Portrait        => "Portrait Actor",
            _                            => "Invalid",
        };
}
