using Dalamud.Interface.ImGuiNotification;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Services;
using OtterGui.Tasks;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.SubMods;
using Penumbra.Services;
using Penumbra.String.Classes;

namespace Penumbra.Mods.Editor;

public class ModNormalizer(ModManager modManager, Configuration config, SaveService saveService) : IService
{
    private readonly List<List<Dictionary<Utf8GamePath, FullPath>>> _redirections = [];

    public  Mod    Mod { get; private set; } = null!;
    private string _normalizationDirName = null!;
    private string _oldDirName           = null!;

    public int  Step       { get; private set; }
    public int  TotalSteps { get; private set; }
    public Task Worker     { get; private set; } = Task.CompletedTask;


    public bool Running
        => !Worker.IsCompleted;

    public void Normalize(Mod mod)
    {
        if (Step < TotalSteps)
            return;

        Mod                   = mod;
        _normalizationDirName = Path.Combine(Mod.ModPath.FullName, "TmpNormalization");
        _oldDirName           = Path.Combine(Mod.ModPath.FullName, "TmpNormalizationOld");
        Step                  = 0;
        TotalSteps            = mod.TotalFileCount + 5;

        Worker = TrackedTask.Run(NormalizeSync);
    }

    public void NormalizeUi(DirectoryInfo modDirectory)
    {
        if (!config.AutoReduplicateUiOnImport)
            return;

        if (modManager.Creator.LoadMod(modDirectory, false, false) is not { } mod)
            return;

        Dictionary<FullPath, List<(IModDataContainer, Utf8GamePath)>> paths      = [];
        Dictionary<IModDataContainer, string>                         containers = [];
        foreach (var container in mod.AllDataContainers)
        {
            foreach (var (gamePath, path) in container.Files)
            {
                if (!gamePath.Path.StartsWith("ui/"u8))
                    continue;

                if (!paths.TryGetValue(path, out var list))
                {
                    list = [];
                    paths.Add(path, list);
                }

                list.Add((container, gamePath));
                containers.TryAdd(container, string.Empty);
            }
        }

        foreach (var container in containers.Keys.ToList())
        {
            if (container.Group == null)
                containers[container] = mod.ModPath.FullName;
            else
            {
                var groupDir  = ModCreator.NewOptionDirectory(mod.ModPath, container.Group.Name, config.ReplaceNonAsciiOnImport);
                var optionDir = ModCreator.NewOptionDirectory(groupDir,    container.GetName(),  config.ReplaceNonAsciiOnImport);
                containers[container] = optionDir.FullName;
            }
        }

        var anyChanges    = 0;
        var modRootLength = mod.ModPath.FullName.Length + 1;
        foreach (var (file, gamePaths) in paths)
        {
            if (gamePaths.Count < 2)
                continue;

            var keptPath = false;
            foreach (var (container, gamePath) in gamePaths)
            {
                var directory   = containers[container];
                var relPath     = new Utf8RelPath(gamePath).ToString();
                var newFilePath = Path.Combine(directory, relPath);
                if (newFilePath == file.FullName)
                {
                    Penumbra.Log.Verbose($"[UIReduplication] Kept {file.FullName[modRootLength..]} because new path was identical.");
                    keptPath = true;
                    continue;
                }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(newFilePath)!);
                    File.Copy(file.FullName, newFilePath, false);
                    Penumbra.Log.Verbose($"[UIReduplication] Copied {file.FullName[modRootLength..]} to {newFilePath[modRootLength..]}.");
                    container.Files[gamePath] = new FullPath(newFilePath);
                    ++anyChanges;
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error(
                        $"[UIReduplication] Failed to copy {file.FullName[modRootLength..]} to {newFilePath[modRootLength..]}:\n{ex}");
                }
            }

            if (keptPath)
                continue;

            try
            {
                File.Delete(file.FullName);
                Penumbra.Log.Verbose($"[UIReduplication] Deleted {file.FullName[modRootLength..]} because no new path matched.");
            }
            catch (Exception ex)
            {
                Penumbra.Log.Error($"[UIReduplication] Failed to delete {file.FullName[modRootLength..]}:\n{ex}");
            }
        }

        if (anyChanges == 0)
            return;

        saveService.Save(SaveType.ImmediateSync, new ModSaveGroup(mod.Default, config.ReplaceNonAsciiOnImport));
        saveService.SaveAllOptionGroups(mod, false, config.ReplaceNonAsciiOnImport);
        Penumbra.Log.Information($"[UIReduplication] Saved groups after {anyChanges} changes.");
    }

    private void NormalizeSync()
    {
        try
        {
            Penumbra.Log.Debug($"[Normalization] Starting Normalization of {Mod.ModPath.Name}...");
            if (!CheckDirectories())
            {
                return;
            }

            Penumbra.Log.Debug("[Normalization] Copying files to temporary directory structure...");
            if (!CopyNewFiles())
            {
                return;
            }

            Penumbra.Log.Debug("[Normalization] Moving old files out of the way...");
            if (!MoveOldFiles())
            {
                return;
            }

            Penumbra.Log.Debug("[Normalization] Moving new directory structure in place...");
            if (!MoveNewFiles())
            {
                return;
            }

            Penumbra.Log.Debug("[Normalization] Applying new redirections...");
            ApplyRedirections();
        }
        catch (Exception e)
        {
            Penumbra.Messager.NotificationMessage(e, $"Could not normalize mod {Mod.Name}.", NotificationType.Error, false);
        }
        finally
        {
            Penumbra.Log.Debug("[Normalization] Cleaning up remaining directories...");
            Cleanup();
        }
    }

    private bool CheckDirectories()
    {
        if (Directory.Exists(_normalizationDirName))
        {
            Penumbra.Messager.NotificationMessage($"Could not normalize mod {Mod.Name}:\n"
              + "The directory TmpNormalization may not already exist when normalizing a mod.", NotificationType.Error, false);
            return false;
        }

        if (Directory.Exists(_oldDirName))
        {
            Penumbra.Messager.NotificationMessage($"Could not normalize mod {Mod.Name}:\n"
              + "The directory TmpNormalizationOld may not already exist when normalizing a mod.", NotificationType.Error, false);
            return false;
        }

        ++Step;
        return true;
    }

    private void Cleanup()
    {
        if (Directory.Exists(_normalizationDirName))
        {
            try
            {
                Directory.Delete(_normalizationDirName, true);
            }
            catch
            {
                // ignored
            }
        }

        if (Directory.Exists(_oldDirName))
        {
            try
            {
                foreach (var dir in new DirectoryInfo(_oldDirName).EnumerateDirectories())
                {
                    dir.MoveTo(Path.Combine(Mod.ModPath.FullName, dir.Name));
                }

                Directory.Delete(_oldDirName, true);
            }
            catch
            {
                // ignored
            }
        }

        Step = TotalSteps;
    }

    private bool CopyNewFiles()
    {
        // We copy all files to a temporary folder to ensure that we can revert the operation on failure.
        try
        {
            var directory = Directory.CreateDirectory(_normalizationDirName);
            for (var i = _redirections.Count; i < Mod.Groups.Count + 1; ++i)
                _redirections.Add([]);

            if (_redirections[0].Count == 0)
                _redirections[0].Add(new Dictionary<Utf8GamePath, FullPath>(Mod.Default.Files.Count));
            else
            {
                _redirections[0][0].Clear();
                _redirections[0][0].EnsureCapacity(Mod.Default.Files.Count);
            }

            // Normalize the default option.
            var newDict = _redirections[0][0];
            foreach (var (gamePath, fullPath) in Mod.Default.Files)
            {
                var relPath      = new Utf8RelPath(gamePath).ToString();
                var newFullPath  = Path.Combine(directory.FullName, relPath);
                var redirectPath = new FullPath(Path.Combine(Mod.ModPath.FullName, relPath));
                Directory.CreateDirectory(Path.GetDirectoryName(newFullPath)!);
                File.Copy(fullPath.FullName, newFullPath, true);
                newDict.Add(gamePath, redirectPath);
                ++Step;
            }

            // Normalize all other options.
            foreach (var (group, groupIdx) in Mod.Groups.WithIndex())
            {
                var groupDir = ModCreator.CreateModFolder(directory, group.Name, config.ReplaceNonAsciiOnImport, true);
                _redirections[groupIdx + 1].EnsureCapacity(group.DataContainers.Count);
                for (var i = _redirections[groupIdx + 1].Count; i < group.DataContainers.Count; ++i)
                    _redirections[groupIdx + 1].Add([]);
                foreach (var (data, dataIdx) in group.DataContainers.WithIndex())
                    HandleSubMod(groupDir, data, _redirections[groupIdx + 1][dataIdx]);
            }

            return true;
        }
        catch (Exception e)
        {
            Penumbra.Messager.NotificationMessage(e, $"Could not normalize mod {Mod.Name}.", NotificationType.Error, false);
        }

        return false;

        void HandleSubMod(DirectoryInfo groupDir, IModDataContainer option, Dictionary<Utf8GamePath, FullPath> newDict)
        {
            var name      = option.GetName();
            var optionDir = ModCreator.CreateModFolder(groupDir, name, config.ReplaceNonAsciiOnImport, true);

            newDict.Clear();
            newDict.EnsureCapacity(option.Files.Count);
            foreach (var (gamePath, fullPath) in option.Files)
            {
                var relPath      = new Utf8RelPath(gamePath).ToString();
                var newFullPath  = Path.Combine(optionDir.FullName, relPath);
                var redirectPath = new FullPath(Path.Combine(Mod.ModPath.FullName, groupDir.Name, optionDir.Name, relPath));
                Directory.CreateDirectory(Path.GetDirectoryName(newFullPath)!);
                File.Copy(fullPath.FullName, newFullPath, true);
                newDict.Add(gamePath, redirectPath);
                ++Step;
            }
        }
    }

    private bool MoveOldFiles()
    {
        try
        {
            // Clean old directories and files.
            var oldDirectory = Directory.CreateDirectory(_oldDirName);
            foreach (var dir in Mod.ModPath.EnumerateDirectories())
            {
                if (dir.FullName.Equals(_oldDirName,           StringComparison.OrdinalIgnoreCase)
                 || dir.FullName.Equals(_normalizationDirName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                dir.MoveTo(Path.Combine(oldDirectory.FullName, dir.Name));
            }

            ++Step;
            return true;
        }
        catch (Exception e)
        {
            Penumbra.Messager.NotificationMessage(e, $"Could not move old files out of the way while normalizing mod {Mod.Name}.",
                NotificationType.Error, false);
        }

        return false;
    }

    private bool MoveNewFiles()
    {
        try
        {
            var mainDir = new DirectoryInfo(_normalizationDirName);
            foreach (var dir in mainDir.EnumerateDirectories())
            {
                dir.MoveTo(Path.Combine(Mod.ModPath.FullName, dir.Name));
            }

            mainDir.Delete();
            Directory.Delete(_oldDirName, true);
            ++Step;
            return true;
        }
        catch (Exception e)
        {
            Penumbra.Messager.NotificationMessage(e, $"Could not move new files into the mod while normalizing mod {Mod.Name}.",
                NotificationType.Error, false);
            foreach (var dir in Mod.ModPath.EnumerateDirectories())
            {
                if (dir.FullName.Equals(_oldDirName,           StringComparison.OrdinalIgnoreCase)
                 || dir.FullName.Equals(_normalizationDirName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    dir.Delete(true);
                }
                catch
                {
                    // ignored
                }
            }
        }

        return false;
    }

    private void ApplyRedirections()
    {
        modManager.OptionEditor.SetFiles(Mod.Default, _redirections[0][0]);
        foreach (var (group, groupIdx) in Mod.Groups.WithIndex())
            foreach (var (container, containerIdx) in group.DataContainers.WithIndex())
                modManager.OptionEditor.SetFiles(container, _redirections[groupIdx + 1][containerIdx]);

        ++Step;
    }
}
