using System.Text.Json;
using ImSharp;
using Luna;
using Penumbra.Mods;

namespace Penumbra.Files;

public static class ModSerialization
{
    public const uint CurrentFileVersion = 4;

    public static void UpdateModOnSave(SaveService files, Utf8JsonWriter j, Mod mod)
    {
        if (mod.LoadedVersion >= 4)
        {
            WriteMod(j, mod);
            return;
        }

        Penumbra.Log.Information($"Migrating mod {mod.Name} from {mod.LoadedVersion} to {CurrentFileVersion}, deleting group files...");
        mod.LoadedVersion = 4;
        foreach (var file in files.FileNames.GetOptionGroupFiles(mod))
            files.DeleteWithBackup(file.FullName);
        WriteMod(j, mod);
    }

    public static void WriteMod(Utf8JsonWriter j, Mod mod)
    {
        j.WriteStartObject();

        j.WriteNumber("FileVersion"u8, CurrentFileVersion);
        j.WriteString("Identifier"u8, mod.StableIdentifier);
        j.WriteString("LastWrite"u8,  DateTime.UtcNow);
        j.WriteString("Name"u8,       mod.Name);
        j.WriteNonEmptyString("Author"u8,      mod.Author);
        j.WriteNonEmptyString("Description"u8, mod.Description);
        j.WriteNonEmptyString("Image"u8,       mod.Image);
        j.WriteNonEmptyString("Version"u8,     mod.Version);
        j.WriteNonEmptyString("Website"u8,     mod.Website);

        if (mod.ModTags.Count > 0)
        {
            j.WriteStartArray("ModTags"u8);
            foreach (var tag in mod.ModTags)
                j.WriteStringValue(tag);
            j.WriteEndArray();
        }

        if (mod.DefaultPreferredItems.Count > 0)
        {
            j.WriteStartArray("DefaultPreferredItems"u8);
            foreach (var item in mod.DefaultPreferredItems)
                j.WriteNumberValue(item);
            j.WriteEndArray();
        }

        if (mod.RequiredFeatures is not FeatureFlags.None)
        {
            var features = mod.RequiredFeatures;
            j.WriteStartArray("RequiredFeatures"u8);
            foreach (var flag in FeatureFlags.Values)
            {
                if ((features & flag) is not FeatureFlags.None)
                    j.WriteStringValue(flag.ToNameU8());
            }

            j.WriteEndArray();
        }

        GroupSerialization.WriteDefaultContainer(j, mod, mod.ModPath);

        if (mod.Groups.Count > 0)
        {
            j.WriteStartArray("Groups"u8);
            foreach (var group in mod.Groups)
                GroupSerialization.WriteGroup(j, group, mod.ModPath);
            j.WriteEndArray();
        }

        j.WriteEndObject();
    }
}
