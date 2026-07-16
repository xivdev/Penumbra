using System.Text.Json;
using ImSharp;
using Luna;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.SubMods;
using Penumbra.UI.Classes;

namespace Penumbra.Files;

public static class GroupSerialization
{
    public static void WriteDefaultContainer(Utf8JsonWriter j, Mod mod, DirectoryInfo? basePath = null)
    {
        if (ContainerEmpty(mod.Default))
            return;

        j.WriteStartObject("DefaultData"u8);
        FillContainer(j, mod.Default, basePath ?? mod.ModPath);
        j.WriteEndObject();
    }

    public static void WriteGroup(Utf8JsonWriter j, IModGroup group, DirectoryInfo? basePath = null)
    {
        j.WriteStartObject();
        FillGroup(j, group, basePath);
        j.WriteEndObject();
    }

    public static void FillGroup(Utf8JsonWriter j, IModGroup group, DirectoryInfo? basePath = null)
    {
        j.WriteString("Type"u8, group.Type.StringU8);
        FillSubObject(j, group);
        j.WriteSignedIfNot("Priority"u8, group.Priority.Value, 0);
        j.WriteNonEmptyString("Image"u8, group.Image);
        j.WriteSignedIfNot("Page"u8, group.Page, 0);
        j.WriteUnsignedIfNot("DefaultSettings"u8, group.DefaultSettings.Value, 0ul);
        if (group.ParentSetting is {} parent)
            j.WriteString("ParentSetting"u8, parent.Id);

        basePath ??= group.Mod.ModPath;
        switch (group)
        {
            case SingleModGroup single:
                if (group.Options.Count > 0)
                {
                    j.WriteStartArray("Options"u8);
                    foreach (var option in single.OptionData)
                    {
                        j.WriteStartObject();
                        FillOption(j, option, basePath);
                        FillContainer(j, option, basePath);
                        j.WriteEndObject();
                    }

                    j.WriteEndArray();
                }

                break;
            case MultiModGroup multi:
                if (group.Options.Count > 0)
                {
                    j.WriteStartArray("Options"u8);
                    foreach (var option in multi.OptionData)
                    {
                        j.WriteStartObject();
                        FillOption(j, option, basePath);
                        j.WriteSignedIfNot("Priority"u8, option.Priority.Value, 0);
                        FillContainer(j, option, basePath);
                        j.WriteEndObject();
                    }

                    j.WriteEndArray();
                }

                break;
            case ImcModGroup imc:
                j.WriteStartObject("Identifier"u8);
                imc.Identifier.AddToJson(j);
                j.WriteEndObject();
                j.WritePropertyName("DefaultEntry"u8);
                imc.DefaultEntry.WriteJson(j);
                j.WriteBoolIf("AllVariants"u8,    imc.AllVariants,    false);
                j.WriteBoolIf("OnlyAttributes"u8, imc.OnlyAttributes, false);
                if (imc.OptionData.Count > 0)
                {
                    j.WriteStartArray("Options"u8);
                    foreach (var option in imc.OptionData)
                    {
                        j.WriteStartObject();
                        FillOption(j, option, basePath);
                        if (option.IsDisableSubMod)
                            j.WriteBoolean("IsDisableSubMod"u8, true);
                        else
                            j.WriteNumber("AttributeMask"u8, option.AttributeMask);
                        j.WriteEndObject();
                    }

                    j.WriteEndArray();
                }

                break;
            case CombiningModGroup combining:
                if (group.Options.Count > 0)
                {
                    j.WriteStartArray("Options"u8);
                    foreach (var option in combining.OptionData)
                    {
                        j.WriteStartObject();
                        FillOption(j, option, basePath);
                        j.WriteEndObject();
                    }

                    j.WriteEndArray();
                }

                j.WriteStartArray("Containers"u8);
                foreach (var container in combining.Data)
                {
                    j.WriteStartObject();
                    j.WriteNonEmptyString("Name"u8, container.Name);
                    FillContainer(j, container, basePath);
                    j.WriteEndObject();
                }

                j.WriteEndArray();
                break;
        }
    }

    public static void FillOption(Utf8JsonWriter j, IModOption option, DirectoryInfo? basePath = null)
    {
        FillSubObject(j, option);
        WriteOptionColor(j, option);
    }

    public static void FillSubObject(Utf8JsonWriter j, IModObject @object)
    {
        j.WriteString("Id"u8,   @object.Id);
        j.WriteString("Name"u8, @object.Name);
        j.WriteNonEmptyString("Description"u8, @object.Description);
        if (@object.Layout is not ModSettingsLayout.None)
        {
            j.WriteStartArray("Layout"u8);
            foreach (var layout in @object.Layout.Iterate())
                j.WriteStringValue(layout.StringU8);
            j.WriteEndArray();
        }

        if (@object.Condition is not null)
        {
            j.WritePropertyName("Condition"u8);
            @object.Condition.WriteJson(j);
        }
    }

    public static void FillContainer(Utf8JsonWriter j, IModDataContainer container, DirectoryInfo basePath)
    {
        if (container.Files.Count > 0)
        {
            j.WriteStartObject("Files"u8);
            foreach (var (gamePath, file) in container.Files)
            {
                if (file.ToRelPath(basePath, out var relPath))
                    j.WriteString(gamePath.Path.Span, relPath.Path.Span);
            }

            j.WriteEndObject();
        }

        if (container.FileSwaps.Count > 0)
        {
            j.WriteStartObject("FileSwaps"u8);
            foreach (var (gamePath, file) in container.FileSwaps)
                j.WriteString(gamePath.Path.Span, file.InternalName.Span);

            j.WriteEndObject();
        }

        if (container.Manipulations.Count > 0)
        {
            j.WritePropertyName("Manipulations"u8);
            MetaSerialization.WriteMetaDictionary(j, container.Manipulations);
        }
    }

    public static bool ContainerEmpty(IModDataContainer? container)
        => container is null || container.Files.Count is 0 && container.FileSwaps.Count is 0 && container.Manipulations.Count is 0;

    private static void WriteOptionColor(Utf8JsonWriter j, IModOption option)
    {
        var c = option.ColorAsInteger;
        if (c is not 0)
            j.WriteNumber("Color"u8, c);
    }
}
