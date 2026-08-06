using System.Text.Json;
using Luna;
using Penumbra.Mods.SubMods;

namespace Penumbra.Mods.Settings;

public readonly record struct ModObjectIdentifier(Guid Identifier, string? Name = null) : IEquatable<ModObjectIdentifier>
{
    public bool IsEmpty
        => Name is null && Identifier == Guid.Empty;

    public ModObjectIdentifier(IModObject @object)
        : this(@object.Id, @object.Name)
    { }

    public ModObjectIdentifier(string name)
        : this(Guid.Empty, name)
    { }

    public bool Matches(ModObjectIdentifier other)
    {
        if (Identifier == Guid.Empty)
        {
            if (Name is null)
                return other.IsEmpty;

            return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
        }

        if (Identifier == other.Identifier)
            return true;
        if (other.Identifier != Guid.Empty)
            return false;

        return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
    }

    public bool Equals(ModObjectIdentifier other)
    {
        if (Identifier != Guid.Empty)
            return Identifier == other.Identifier;

        if (other.Identifier != Guid.Empty)
            return false;

        return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        if (Identifier != Guid.Empty)
            return Identifier.GetHashCode();
        if (Name is null)
            return 0;

        return Name.GetHashCode(StringComparison.OrdinalIgnoreCase);
    }

    public IModObject? Find(Mod mod)
    {
        if (Identifier != Guid.Empty && mod.SubObjects.TryGetValue(Identifier, out var @object))
            return @object;

        if (Name is not { } name)
            return null;

        return mod.SubObjects.Values.FirstOrDefault(o => o.Name == name);
    }

    public void WriteJson(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        AddToJson(writer);
        writer.WriteEndObject();
    }

    public void AddToJson(Utf8JsonWriter writer)
    {
        writer.WriteString("Identifier"u8, Identifier);
        writer.WriteNonEmptyString("Name"u8, Name);
    }

    public static bool ReadJson(ref Utf8JsonReader reader, ref Guid? guid, ref string? name)
    {
        if (reader.GuidProperty("Identifier"u8, out var g))
        {
            guid = g;
            return true;
        }

        if (reader.StringProperty("Name"u8, out string? n, true))
        {
            name = n;
            return true;
        }

        return false;
    }
}
