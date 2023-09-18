using OtterGui.Classes;
using Penumbra.Collections;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Subclasses;
using Penumbra.Services;
using Penumbra.String.Classes;

namespace Penumbra.Mods;

public class TemporaryMod : IMod
{
    public LowerString Name     { get; init; } = LowerString.Empty;
    public int         Index    { get; init; } = -2;
    public int         Priority { get; init; } = int.MaxValue;

    public int TotalManipulations
        => Default.Manipulations.Count;

    public readonly SubMod Default;

    ISubMod IMod.Default
        => Default;

    public IReadOnlyList<IModGroup> Groups
        => Array.Empty<IModGroup>();

    public IEnumerable<SubMod> AllSubMods
        => new[]
        {
            Default,
        };

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
            dir = ModCreator.CreateModFolder(modManager.BasePath, collection.Name);
            var fileDir = Directory.CreateDirectory(Path.Combine(dir.FullName, "files"));
            modManager.DataEditor.CreateMeta(dir, collection.Name, character ?? config.DefaultModAuthor,
                $"Mod generated from temporary collection {collection.Name} for {character ?? "Unknown Character"}.", null, null);
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

            saveService.ImmediateSave(new ModSaveGroup(dir, defaultMod));
            modManager.AddMod(dir);
            Penumbra.Log.Information($"Successfully generated mod {mod.Name} at {mod.ModPath.FullName} for collection {collection.Name}.");
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not save temporary collection {collection.Name} to permanent Mod:\n{e}");
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
