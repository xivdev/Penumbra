using OtterGui.Classes;
using Penumbra.Collections;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Subclasses;
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

    public readonly SubMod Default;

    public AppliedModData GetData(ModSettings? settings = null)
    {
        Dictionary<Utf8GamePath, FullPath> dict;
        if (Default.FileSwapData.Count == 0)
        {
            dict = Default.FileData;
        }
        else if (Default.FileData.Count == 0)
        {
            dict = Default.FileSwapData;
        }
        else
        {
            // Need to ensure uniqueness.
            dict = new Dictionary<Utf8GamePath, FullPath>(Default.FileData.Count + Default.FileSwaps.Count);
            foreach (var (gamePath, file) in Default.FileData.Concat(Default.FileSwaps))
                dict.TryAdd(gamePath, file);
        }

        return new AppliedModData(dict, Default.Manipulations);
    }

    ISubMod IMod.Default
        => Default;

    public IReadOnlyList<IModGroup> Groups
        => Array.Empty<IModGroup>();

    public IEnumerable<SubMod> AllSubMods
        => [Default];

    public TemporaryMod()
        => Default = new SubMod(this);

    public void SetFile(Utf8GamePath gamePath, FullPath fullPath)
        => Default.FileData[gamePath] = fullPath;

    public bool SetManipulation(MetaManipulation manip)
        => Default.ManipulationData.Remove(manip) | Default.ManipulationData.Add(manip);

    public void SetAll(Dictionary<Utf8GamePath, FullPath> dict, HashSet<MetaManipulation> manips)
    {
        Default.FileData         = dict;
        Default.ManipulationData = manips;
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
                if (fullPath.Path.Name.StartsWith('|'))
                {
                    targetPath = targetPath.Split('|', 3, StringSplitOptions.RemoveEmptyEntries).Last();
                }

                if (Path.IsPathRooted(targetPath))
                {
                    var target = Path.Combine(fileDir.FullName, Path.GetFileName(targetPath));
                    File.Copy(targetPath, target, true);
                    defaultMod.FileData[gamePath] = new FullPath(target);
                }
                else
                {
                    defaultMod.FileSwapData[gamePath] = new FullPath(targetPath);
                }
            }

            foreach (var manip in collection.MetaCache?.Manipulations ?? Array.Empty<MetaManipulation>())
                defaultMod.ManipulationData.Add(manip);

            saveService.ImmediateSave(new ModSaveGroup(dir, defaultMod, config.ReplaceNonAsciiOnImport));
            modManager.AddMod(dir);
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
