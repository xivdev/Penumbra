using OtterGui.Classes;
using Penumbra.Collections;
using Penumbra.Interop.PathResolving;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.Services;
using Penumbra.String.Classes;

namespace Penumbra.Mods;

public class TemporaryMod : IMod
{
    public LowerString Name     { get; init; } = LowerString.Empty;
    public int         Index    { get; init; } = -2;
    public ModPriority Priority { get; init; } = ModPriority.MaxValue;

    public int TotalManipulations
        => Default.Manipulations.Count;

    public readonly DefaultSubMod Default;

    public AppliedModData GetData(ModSettings? settings = null)
    {
        Dictionary<Utf8GamePath, FullPath> dict;
        if (Default.FileSwaps.Count == 0)
        {
            dict = Default.Files;
        }
        else if (Default.Files.Count == 0)
        {
            dict = Default.FileSwaps;
        }
        else
        {
            // Need to ensure uniqueness.
            dict = new Dictionary<Utf8GamePath, FullPath>(Default.Files.Count + Default.FileSwaps.Count);
            foreach (var (gamePath, file) in Default.Files.Concat(Default.FileSwaps))
                dict.TryAdd(gamePath, file);
        }

        return new AppliedModData(dict, Default.Manipulations);
    }

    public IReadOnlyList<IModGroup> Groups
        => Array.Empty<IModGroup>();

    public TemporaryMod()
        => Default = new DefaultSubMod(this);

    public void SetAll(Dictionary<Utf8GamePath, FullPath> dict, MetaDictionary manips)
    {
        Default.Files         = dict;
        Default.Manipulations = manips;
    }

    public static void SaveTempCollection(Configuration config, SaveService saveService, ModManager modManager, ModCollection collection,
        string? character = null)
    {
        DirectoryInfo? dir = null;
        try
        {
            dir = ModCreator.CreateModFolder(modManager.BasePath, collection.Name, config.ReplaceNonAsciiOnImport, true);
            var fileDir = Directory.CreateDirectory(Path.Combine(dir.FullName, "files"));
            modManager.DataEditor.CreateMeta(dir, collection.Name, character ?? config.DefaultModAuthor,
                $"Mod generated from temporary collection {collection.Id} for {character ?? "Unknown Character"} with name {collection.Name}.",
                null, null);
            var mod        = new Mod(dir);
            var defaultMod = mod.Default;
            foreach (var (gamePath, fullPath) in collection.ResolvedFiles)
            {
                if (gamePath.Path.EndsWith(".imc"u8))
                {
                    continue;
                }

                var targetPath = fullPath.Path.FullName;
                if (PathDataHandler.Split(fullPath.Path.FullName, out var actualPath, out _))
                    targetPath = actualPath.ToString();

                if (Path.IsPathRooted(targetPath))
                {
                    var target = Path.Combine(fileDir.FullName, Path.GetFileName(targetPath));
                    File.Copy(targetPath, target, true);
                    defaultMod.Files[gamePath] = new FullPath(target);
                }
                else
                {
                    defaultMod.FileSwaps[gamePath] = new FullPath(targetPath);
                }
            }

            var manips = new MetaDictionary(collection.MetaCache);
            defaultMod.Manipulations.UnionWith(manips);

            saveService.ImmediateSaveSync(new ModSaveGroup(dir, defaultMod, config.ReplaceNonAsciiOnImport));
            modManager.AddMod(dir, false);
            Penumbra.Log.Information(
                $"Successfully generated mod {mod.Name} at {mod.ModPath.FullName} for collection {collection.Identifier}.");
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not save temporary collection {collection.Identifier} to permanent Mod:\n{e}");
            if (dir != null && Directory.Exists(dir.FullName))
            {
                try
                {
                    Directory.Delete(dir.FullName, true);
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}
