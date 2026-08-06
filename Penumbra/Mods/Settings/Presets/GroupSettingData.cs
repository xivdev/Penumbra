using System.Text.Json;
using Luna;
using Penumbra.Mods.Groups;

namespace Penumbra.Mods.Settings;

public readonly struct GroupSettingData() : IEquatable<GroupSettingData>
{
    public GroupSettingData(IModGroup group, Setting setting)
        : this()
    {
        Update(group, setting);
    }

    public void Update(IModGroup group, Setting setting)
    {
        Enabled.Clear();
        Disabled.Clear();

        if (group.Behaviour is GroupDrawBehaviour.SingleSelection)
        {
            Disabled.EnsureCapacity(group.Options.Count - 1);
            if (setting.AsIndex >= group.Options.Count)
                setting = group.DefaultSettings;
            if (setting.AsIndex >= group.Options.Count)
                setting = Setting.Zero;
            foreach (var option in group.Options)
            {
                var identifier = new ModObjectIdentifier(option);
                if (Ignored.Contains(identifier))
                    continue;

                if (setting.AsIndex == option.Index)
                    Enabled.Add(identifier);
                else
                    Ignored.Add(identifier);
            }
        }
        else
        {
            var popCount = BitOperations.PopCount(setting.Value);
            Enabled.EnsureCapacity(popCount);
            popCount = group.Options.Count - popCount;
            if (popCount > 0)
                Disabled.EnsureCapacity(popCount);
            foreach (var option in group.Options)
            {
                var identifier = new ModObjectIdentifier(option);
                if (Ignored.Contains(identifier))
                    continue;

                if (setting.HasFlag(option.Index))
                    Enabled.Add(identifier);
                else
                    Disabled.Add(identifier);
            }
        }
    }

    public bool SetIdentifier(ModObjectIdentifier identifier, bool? value)
        => value switch
        {
            null  => Ignored.Add(identifier) | Enabled.Remove(identifier) | Disabled.Remove(identifier),
            true  => Ignored.Remove(identifier) | Enabled.Add(identifier) | Disabled.Remove(identifier),
            false => Ignored.Remove(identifier) | Enabled.Remove(identifier) | Disabled.Add(identifier),
        };

    public bool MakeGeneric()
    {
        var ret = HandleSet(Enabled);
        ret |= HandleSet(Disabled);
        ret |= HandleSet(Ignored);
        // Those can only affect anything if we had changes before,
        // so we do not need to check this.
        Enabled.ExceptWith(Ignored);
        Disabled.ExceptWith(Ignored);
        Disabled.ExceptWith(Enabled);
        return ret;

        static bool HandleSet(HashSet<ModObjectIdentifier> set)
        {
            var tmp = set.ToHashSet();
            set.Clear();
            var changes = false;
            foreach (var id in tmp)
            {
                changes |= id.Identifier != Guid.Empty;
                if (id.Name is null)
                {
                    changes = true;
                    continue;
                }

                changes |= !set.Add(new ModObjectIdentifier(id.Name));
            }

            return changes;
        }
    }

    public readonly HashSet<ModObjectIdentifier> Enabled  = [];
    public readonly HashSet<ModObjectIdentifier> Disabled = [];
    public readonly HashSet<ModObjectIdentifier> Ignored  = [];

    public bool Equals(GroupSettingData other)
        => Enabled.SetEquals(other.Enabled)
         && Disabled.SetEquals(other.Disabled)
         && Ignored.SetEquals(other.Ignored);

    public override bool Equals(object? obj)
        => obj is GroupSettingData other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(Enabled.Count, Disabled.Count, Ignored.Count);

    public static bool operator ==(GroupSettingData left, GroupSettingData right)
        => left.Equals(right);

    public static bool operator !=(GroupSettingData left, GroupSettingData right)
        => !left.Equals(right);

    public bool ReadJson(ref Utf8JsonReader j)
    {
        if (j.ArrayProperty("Enabled"u8, out var enabled))
        {
            ReadSet(ref j, enabled, Enabled);
            Disabled.ExceptWith(Enabled);
            Ignored.ExceptWith(Enabled);
            return true;
        }

        if (j.ArrayProperty("Disabled"u8, out var disabled))
        {
            ReadSet(ref j, disabled, Disabled);
            Enabled.ExceptWith(Disabled);
            Ignored.ExceptWith(Disabled);
            return true;
        }

        if (j.ArrayProperty("Ignored"u8, out var ignored))
        {
            ReadSet(ref j, ignored, Ignored);
            Enabled.ExceptWith(Ignored);
            Disabled.ExceptWith(Ignored);
            return true;
        }

        return false;
    }

    private static void ReadSet(ref Utf8JsonReader j, Utf8JsonObjectLimit limit, HashSet<ModObjectIdentifier> set)
    {
        set.Clear();
        while (limit.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.StartObject)
                continue;

            var     obj  = j.CreateObjectLimit();
            Guid?   guid = null;
            string? name = null;
            while (obj.Read(ref j))
            {
                if (!ModObjectIdentifier.ReadJson(ref j, ref guid, ref name))
                    j.Skip();
            }

            if (guid is null)
                continue;

            set.Add(new ModObjectIdentifier(guid.Value, name));
        }
    }
}
