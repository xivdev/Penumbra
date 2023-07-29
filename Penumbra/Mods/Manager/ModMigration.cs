using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui;
using Penumbra.Api.Enums;
using Penumbra.Mods.Subclasses;
using Penumbra.Services;
using Penumbra.String.Classes;
using Penumbra.Util;

namespace Penumbra.Mods.Manager;

public static partial class ModMigration
{
    [GeneratedRegex(@"group_\d{3}_", RegexOptions.Compiled | RegexOptions.NonBacktracking | RegexOptions.ExplicitCapture)]
    private static partial Regex GroupRegex();

    [GeneratedRegex("^group_", RegexOptions.Compiled)]
    private static partial Regex GroupStartRegex();

    public static bool Migrate(ModCreator creator, SaveService saveService, Mod mod, JObject json, ref uint fileVersion)
        => MigrateV0ToV1(creator, saveService, mod, json, ref fileVersion) || MigrateV1ToV2(saveService, mod, ref fileVersion) || MigrateV2ToV3(mod, ref fileVersion);

    private static bool MigrateV2ToV3(Mod _, ref uint fileVersion)
    {
        if (fileVersion > 2)
            return false;

        // Remove import time.
        fileVersion = 3;
        return true;
    }

    private static bool MigrateV1ToV2(SaveService saveService, Mod mod, ref uint fileVersion)
    {
        if (fileVersion > 1)
            return false;

        if (!saveService.FileNames.GetOptionGroupFiles(mod).All(g => GroupRegex().IsMatch(g.Name)))
            foreach (var (group, index) in saveService.FileNames.GetOptionGroupFiles(mod).WithIndex().ToArray())
            {
                var newName = GroupStartRegex().Replace(group.Name, $"group_{index + 1:D3}_");
                try
                {
                    if (newName != group.Name)
                        group.MoveTo(Path.Combine(group.DirectoryName ?? string.Empty, newName), false);
                }
                catch (Exception e)
                {
                    Penumbra.Log.Error($"Could not rename group file {group.Name} to {newName} during migration:\n{e}");
                }
            }

        fileVersion = 2;

        return true;
    }

    private static bool MigrateV0ToV1(ModCreator creator, SaveService saveService, Mod mod, JObject json, ref uint fileVersion)
    {
        if (fileVersion > 0)
            return false;

        var swaps = json["FileSwaps"]?.ToObject<Dictionary<Utf8GamePath, FullPath>>()
         ?? new Dictionary<Utf8GamePath, FullPath>();
        var groups = json["Groups"]?.ToObject<Dictionary<string, OptionGroupV0>>() ?? new Dictionary<string, OptionGroupV0>();
        var priority = 1;
        var seenMetaFiles = new HashSet<FullPath>();
        foreach (var group in groups.Values)
            ConvertGroup(creator, mod, group, ref priority, seenMetaFiles);

        foreach (var unusedFile in mod.FindUnusedFiles().Where(f => !seenMetaFiles.Contains(f)))
        {
            if (unusedFile.ToGamePath(mod.ModPath, out var gamePath)
             && !mod.Default.FileData.TryAdd(gamePath, unusedFile))
                Penumbra.Log.Error($"Could not add {gamePath} because it already points to {mod.Default.FileData[gamePath]}.");
        }

        mod.Default.FileSwapData.Clear();
        mod.Default.FileSwapData.EnsureCapacity(swaps.Count);
        foreach (var (gamePath, swapPath) in swaps)
            mod.Default.FileSwapData.Add(gamePath, swapPath);

        creator.IncorporateMetaChanges(mod.Default, mod.ModPath, true);
        foreach (var (_, index) in mod.Groups.WithIndex())
            saveService.ImmediateSave(new ModSaveGroup(mod, index));

        // Delete meta files.
        foreach (var file in seenMetaFiles.Where(f => f.Exists))
        {
            try
            {
                File.Delete(file.FullName);
            }
            catch (Exception e)
            {
                Penumbra.Log.Warning($"Could not delete meta file {file.FullName} during migration:\n{e}");
            }
        }

        // Delete old meta files.
        var oldMetaFile = Path.Combine(mod.ModPath.FullName, "metadata_manipulations.json");
        if (File.Exists(oldMetaFile))
            try
            {
                File.Delete(oldMetaFile);
            }
            catch (Exception e)
            {
                Penumbra.Log.Warning($"Could not delete old meta file {oldMetaFile} during migration:\n{e}");
            }

        fileVersion = 1;
        saveService.ImmediateSave(new ModSaveGroup(mod, -1));

        return true;
    }

    private static void ConvertGroup(ModCreator creator, Mod mod, OptionGroupV0 group, ref int priority, HashSet<FullPath> seenMetaFiles)
    {
        if (group.Options.Count == 0)
            return;

        switch (group.SelectionType)
        {
            case GroupType.Multi:

                var optionPriority = 0;
                var newMultiGroup = new MultiModGroup()
                {
                    Name = group.GroupName,
                    Priority = priority++,
                    Description = string.Empty,
                };
                mod.Groups.Add(newMultiGroup);
                foreach (var option in group.Options)
                    newMultiGroup.PrioritizedOptions.Add((SubModFromOption(creator, mod, option, seenMetaFiles), optionPriority++));

                break;
            case GroupType.Single:
                if (group.Options.Count == 1)
                {
                    AddFilesToSubMod(mod.Default, mod.ModPath, group.Options[0], seenMetaFiles);
                    return;
                }

                var newSingleGroup = new SingleModGroup()
                {
                    Name = group.GroupName,
                    Priority = priority++,
                    Description = string.Empty,
                };
                mod.Groups.Add(newSingleGroup);
                foreach (var option in group.Options)
                    newSingleGroup.OptionData.Add(SubModFromOption(creator, mod, option, seenMetaFiles));

                break;
        }
    }

    private static void AddFilesToSubMod(SubMod mod, DirectoryInfo basePath, OptionV0 option, HashSet<FullPath> seenMetaFiles)
    {
        foreach (var (relPath, gamePaths) in option.OptionFiles)
        {
            var fullPath = new FullPath(basePath, relPath);
            foreach (var gamePath in gamePaths)
                mod.FileData.TryAdd(gamePath, fullPath);

            if (fullPath.Extension is ".meta" or ".rgsp")
                seenMetaFiles.Add(fullPath);
        }
    }

    private static SubMod SubModFromOption(ModCreator creator, Mod mod, OptionV0 option, HashSet<FullPath> seenMetaFiles)
    {
        var subMod = new SubMod(mod) { Name = option.OptionName };
        AddFilesToSubMod(subMod, mod.ModPath, option, seenMetaFiles);
        creator.IncorporateMetaChanges(subMod, mod.ModPath, false);
        return subMod;
    }

    private struct OptionV0
    {
        public string OptionName = string.Empty;
        public string OptionDesc = string.Empty;

        [JsonProperty(ItemConverterType = typeof(SingleOrArrayConverter<Utf8GamePath>))]
        public Dictionary<Utf8RelPath, HashSet<Utf8GamePath>> OptionFiles = new();

        public OptionV0()
        { }
    }

    private struct OptionGroupV0
    {
        public string GroupName = string.Empty;

        [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public GroupType SelectionType = GroupType.Single;

        public List<OptionV0> Options = new();

        public OptionGroupV0()
        { }
    }

    // Not used anymore, but required for migration.
    private class SingleOrArrayConverter<T> : JsonConverter
    {
        public override bool CanConvert(Type objectType)
            => objectType == typeof(HashSet<T>);

        public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);

            if (token.Type == JTokenType.Array)
                return token.ToObject<HashSet<T>>() ?? new HashSet<T>();

            var tmp = token.ToObject<T>();
            return tmp != null
                ? new HashSet<T> { tmp }
                : new HashSet<T>();
        }

        public override bool CanWrite
            => true;

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            if (value != null)
            {
                var v = (HashSet<T>)value;
                foreach (var val in v)
                    serializer.Serialize(writer, val?.ToString());
            }

            writer.WriteEndArray();
        }
    }
}
