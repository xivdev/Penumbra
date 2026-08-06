using System.Text.Json;
using Luna;
using Penumbra.Util;

namespace Penumbra.Mods.Settings;

public sealed class SettingsDictionary : Dictionary<ModObjectIdentifier, GroupSettingData>
{
    public SettingsDictionary()
    { }

    public SettingsDictionary(Mod mod, ModSettings? settings)
        : base(mod.Groups.Count)
    {
        foreach (var group in mod.Groups)
        {
            var config = settings is null || settings.IsEmpty ? group.DefaultSettings : settings.Settings[group.Index];
            TryAdd(new ModObjectIdentifier(group), new GroupSettingData(group, config));
        }
    }

    public bool MakeGeneric()
    {
        var changes = false;
        var oldDict = this.ToArray();
        Clear();
        foreach (var (id, data) in oldDict)
        {
            if (id.Identifier == Guid.Empty)
                continue;

            if (id.Name is null)
            {
                changes = true;
                continue;
            }

            changes |= data.MakeGeneric();
            changes |= TryAdd(new ModObjectIdentifier(id.Name), data);
        }

        return changes;
    }

    public bool SetIdentifier(ModObjectIdentifier group, ModObjectIdentifier setting, bool? value)
    {
        var ret = false;
        if (!TryGetValue(group, out var data))
        {
            data = new GroupSettingData();
            Add(group, data);
            ret = true;
        }

        return ret | data.SetIdentifier(setting, value);
    }

    public bool ReplaceGroupIdentifier(Guid identifier, string? name, Guid newIdentifier, string? newName)
    {
        var old  = new ModObjectIdentifier(identifier,    name);
        var @new = new ModObjectIdentifier(newIdentifier, newName);
        if (old.Equals(@new))
            return false;

        if (!Remove(old, out var data))
            return false;

        this[@new] = data;
        return true;
    }

    public bool ReplaceOptionIdentifiers(Guid identifier, string? name, Guid newIdentifier, string? newName, ModObjectIdentifier? groupId)
    {
        var old  = new ModObjectIdentifier(identifier,    name);
        var @new = new ModObjectIdentifier(newIdentifier, newName);
        if (old.Equals(@new))
            return false;

        if (groupId is null)
            return Values.Aggregate(false, (current, data) => current | ChangeIdentifier(data));

        return TryGetValue(groupId.Value, out var set) && ChangeIdentifier(set);

        bool ChangeIdentifier(in GroupSettingData data)
        {
            if (data.Enabled.Remove(old))
            {
                data.Enabled.Add(@new);
                return true;
            }

            if (data.Disabled.Remove(old))
            {
                data.Disabled.Add(@new);
                return true;
            }

            if (data.Ignored.Remove(old))
            {
                data.Ignored.Add(@new);
                return true;
            }

            return false;
        }
    }

    public bool Equals(SettingsDictionary other)
        => other.SetEquals(this);

    public void WriteJson(Utf8JsonWriter writer)
    {
        writer.WriteStartArray();
        foreach (var (group, data) in this)
        {
            writer.WriteStartObject();
            group.AddToJson(writer);
            WriteArray(writer, "Enabled"u8,  data.Enabled);
            WriteArray(writer, "Disabled"u8, data.Disabled);
            WriteArray(writer, "Ignored"u8,  data.Ignored);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    public void ReadJson(ref Utf8JsonReader j, Utf8JsonObjectLimit array)
    {
        Clear();
        while (array.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.StartObject)
                continue;

            var              group           = j.CreateObjectLimit();
            Guid?            groupIdentifier = null;
            string?          groupName       = null;
            GroupSettingData groupData       = new();

            while (group.Read(ref j))
            {
                if (j.TokenType is not JsonTokenType.PropertyName)
                    continue;

                if (j.GuidProperty("GroupIdentifier"u8, out var gi))
                    groupIdentifier = gi;
                else if (j.StringProperty("GroupName"u8, out string? gn, true))
                    groupName = gn;
                else if (groupData.ReadJson(ref j))
                    ;
                else
                    j.Skip();
            }

            if (!groupIdentifier.HasValue)
                continue;

            TryAdd(new ModObjectIdentifier(groupIdentifier.Value, groupName), groupData);
        }
    }

    private static void WriteArray(Utf8JsonWriter writer, ReadOnlySpan<byte> property, IReadOnlyCollection<ModObjectIdentifier> data)
    {
        if (data.Count is 0)
            return;

        writer.WriteStartArray(property);
        foreach (var option in data)
            option.WriteJson(writer);
        writer.WriteEndArray();
    }
}
