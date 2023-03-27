using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Interface.Internal.Notifications;
using OtterGui;
using Penumbra.String.Classes;

namespace Penumbra.Mods;

public class ModNormalizer
{
    private readonly ModManager                                     _modManager;
    private readonly List<List<Dictionary<Utf8GamePath, FullPath>>> _redirections = new();

    public  Mod    Mod { get; private set; } = null!;
    private string _normalizationDirName = null!;
    private string _oldDirName           = null!;

    public int Step       { get; private set; }
    public int TotalSteps { get; private set; }

    public bool Running
        => Step < TotalSteps;

    public ModNormalizer(ModManager modManager)
        => _modManager = modManager;

    public void Normalize(Mod mod)
    {
        if (Step < TotalSteps)
            return;

        Mod                   = mod;
        _normalizationDirName = Path.Combine(Mod.ModPath.FullName, "TmpNormalization");
        _oldDirName           = Path.Combine(Mod.ModPath.FullName, "TmpNormalizationOld");
        Step                  = 0;
        TotalSteps            = mod.TotalFileCount + 5;

        Task.Run(NormalizeSync);
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
            Penumbra.ChatService.NotificationMessage($"Could not normalize mod:\n{e}", "Failure", NotificationType.Error);
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
            Penumbra.ChatService.NotificationMessage("Could not normalize mod:\n"
              + "The directory TmpNormalization may not already exist when normalizing a mod.", "Failure",
                NotificationType.Error);
            return false;
        }

        if (Directory.Exists(_oldDirName))
        {
            Penumbra.ChatService.NotificationMessage("Could not normalize mod:\n"
              + "The directory TmpNormalizationOld may not already exist when normalizing a mod.", "Failure",
                NotificationType.Error);
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
                _redirections.Add(new List<Dictionary<Utf8GamePath, FullPath>>());

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
                _redirections[groupIdx + 1].EnsureCapacity(group.Count);
                for (var i = _redirections[groupIdx + 1].Count; i < group.Count; ++i)
                    _redirections[groupIdx + 1].Add(new Dictionary<Utf8GamePath, FullPath>());

                var groupDir = Mod.Creator.CreateModFolder(directory, group.Name);
                foreach (var option in group.OfType<SubMod>())
                {
                    var optionDir = Mod.Creator.CreateModFolder(groupDir, option.Name);

                    newDict = _redirections[groupIdx + 1][option.OptionIdx];
                    newDict.Clear();
                    newDict.EnsureCapacity(option.FileData.Count);
                    foreach (var (gamePath, fullPath) in option.FileData)
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

            return true;
        }
        catch (Exception e)
        {
            Penumbra.ChatService.NotificationMessage($"Could not normalize mod:\n{e}", "Failure", NotificationType.Error);
        }

        return false;
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
            Penumbra.ChatService.NotificationMessage($"Could not move old files out of the way while normalizing mod mod:\n{e}", "Failure",
                NotificationType.Error);
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
            Penumbra.ChatService.NotificationMessage($"Could not move new files into the mod while normalizing mod mod:\n{e}", "Failure",
                NotificationType.Error);
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
        foreach (var option in Mod.AllSubMods.OfType<SubMod>())
            _modManager.OptionEditor.OptionSetFiles(Mod, option.GroupIdx, option.OptionIdx,
                _redirections[option.GroupIdx + 1][option.OptionIdx]);

        ++Step;
    }
}
