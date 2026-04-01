using System.Collections.Frozen;
using Luna;
using Penumbra.Mods;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.UI.ManagementTab;

public sealed class ReservedFiles(ModGroupEditor editor, ManagementLog<ReservedFiles> log) : IService
{
    public static readonly FrozenDictionary<uint, CiByteString> Files = (((uint, CiByteString)[])
    [
        (0x90E4EE2F, new CiByteString("common/graphics/texture/dummy.tex"u8,    MetaDataComputation.All)),
        (0x84815A1A, new CiByteString("chara/common/texture/white.tex"u8,       MetaDataComputation.All)),
        (0x749091FB, new CiByteString("chara/common/texture/black.tex"u8,       MetaDataComputation.All)),
        (0x5CB9681A, new CiByteString("chara/common/texture/id_16.tex"u8,       MetaDataComputation.All)),
        (0x2A583051, new CiByteString("chara/common/texture/common_id.tex"u8,   MetaDataComputation.All)),
        (0x7E78D000, new CiByteString("chara/common/texture/red.tex"u8,         MetaDataComputation.All)),
        (0xBDC0BFD3, new CiByteString("chara/common/texture/green.tex"u8,       MetaDataComputation.All)),
        (0xC410E850, new CiByteString("chara/common/texture/blue.tex"u8,        MetaDataComputation.All)),
        (0xD5CFA221, new CiByteString("chara/common/texture/null_normal.tex"u8, MetaDataComputation.All)),
        (0xBE48CA67, new CiByteString("chara/common/texture/skin_mask.tex"u8,   MetaDataComputation.All)),
    ]).ToFrozenDictionary(p => p.Item1, p =>
    {
        Debug.Assert((uint)p.Item2.Crc32 == p.Item1,
            $"Invalid hash computation in reserved files for {p.Item2} ({p.Item1:X} vs {p.Item2.Crc32:X}).");
        return p.Item2;
    });

    private const bool DryRun = false;


    public void DeleteItem(ReservedFileRedirection item)
    {
        if (!item.Container.TryGetTarget(out var container))
            return;

        if (item.FileSwap)
        {
            var swaps = container.FileSwaps.ToDictionary();
            if (!swaps.Remove(item.GamePath, out _))
                return;

            if (!DryRun)
                editor.SetFileSwaps(container, swaps);
            log.Information(
                $"Removed reserved file swap {item.GamePath} -> {item.FilePath} in {container.Mod.Name} - {container.GetFullName()}.");
        }
        else
        {
            var redirections = container.Files.ToDictionary();
            if (!redirections.Remove(item.GamePath, out var file))
                return;

            if (!DryRun)
                editor.SetFiles(container, redirections);
            log.Information(
                $"Removed reserved file redirection {item.GamePath} -> {item.FilePath} in {container.Mod.Name} - {container.GetFullName()}.");
            if (file.Exists && ((Mod)container.Mod).AllDataContainers.All(c => !c.Files.ContainsValue(file)))
                DeleteFile(file.FullName);
        }
    }

    public void RemoveRedundant(ScannerTabCache<ReservedFileCacheObject, ReservedFileRedirection> cache, bool removeAll)
    {
        log.Information($"Removing all {cache.Count} {(removeAll ? "reserved" : "redundant reserved")} file redirections...");
        var files   = new SetDictionary<Mod, FullPath>();
        var indices = new List<int>(cache.AllItems.Count);
        foreach (var group in cache.AllItems.Select((r, i) => (r.ScannedObject, i))
                     .GroupBy(r => r.ScannedObject.Container.TryGetTarget(out var container) ? container : null).ToList())
        {
            if (group.Key is not { } container)
                continue;

            var swaps        = container.FileSwaps.ToDictionary();
            var redirections = container.Files.ToDictionary();
            var different    = 0;

            foreach (var (redirection, index) in group)
            {
                var containerName = container.GetFullName();
                if (redirection.FileSwap)
                {
                    swaps.Remove(redirection.GamePath);
                    log.Information(
                        $"Removed reserved file swap {redirection.GamePath} -> {redirection.FilePath} in {container.Mod.Name} - {containerName}.");
                    indices.Add(index);
                }
                else if (redirection.ConceptuallyEqual || redirection.Missing || redirection.Broken)
                {
                    var type = redirection.Broken ? "broken." : redirection.Missing ? "missing." : "conceptually equal.";
                    redirections.Remove(redirection.GamePath, out var file);
                    files.TryAdd((Mod)container.Mod, file);
                    log.Information(
                        $"Removed reserved file redirection {redirection.GamePath} -> {redirection.FilePath} in {container.Mod.Name} - {containerName} because the target file was {type}");
                    indices.Add(index);
                }
                else if (removeAll)
                {
                    ++different;
                    redirections.Remove(redirection.GamePath, out var file);
                    files.TryAdd((Mod)container.Mod, file);
                    log.Information(
                        $"Removed reserved file redirection {redirection.GamePath} -> {redirection.FilePath} in {container.Mod.Name} - {containerName} despite the target file being different from the source.");
                    indices.Add(index);
                }
            }

            if (swaps.Count < container.FileSwaps.Count)
            {
                log.Information(
                    $"Removed {container.FileSwaps.Count - swaps.Count} reserved file swaps in {container.Mod.Name} - {container.GetFullName()}.");
                if (!DryRun)
                    editor.SetFileSwaps(container, swaps);
            }

            if (redirections.Count < container.Files.Count)
            {
                log.Information(
                    $"Removed {container.Files.Count - redirections.Count} reserved file redirections in {container.Mod.Name} - {container.GetFullName()}{(different > 0 ? $" (including {different} Differing redirections)." : ".")}");
                if (!DryRun)
                    editor.SetFiles(container, redirections);
            }
        }

        indices.Sort();
        foreach (var idx in indices.AsEnumerable().Reverse())
            cache.DeleteSingleItem(idx);

        foreach (var (mod, modFiles) in files.Grouped)
        {
            var collectedUsage = mod.AllDataContainers.SelectMany(m => m.Files.Values).ToHashSet();
            foreach (var file in modFiles)
            {
                if (!file.Exists)
                    continue;

                if (!collectedUsage.Contains(file))
                    DeleteFile(file.FullName);
            }
        }
    }

    private void DeleteFile(string filePath)
    {
        try
        {
            if (!DryRun)
                File.Delete(filePath);
            log.Information($"Deleted now unused file {filePath} after removing it from reserved file redirections.");
        }
        catch (Exception ex)
        {
            log.Error($"Unable to delete reserved file {filePath} removed from all redirections:\n{ex}");
        }
    }
}
