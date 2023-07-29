using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Subclasses;
using Penumbra.Services;
using Penumbra.String.Classes;

namespace Penumbra.Mods.Editor;

public class DuplicateManager
{
    private readonly SaveService                                      _saveService;
    private readonly ModManager                                       _modManager;
    private readonly SHA256                                           _hasher     = SHA256.Create();
    private readonly List<(FullPath[] Paths, long Size, byte[] Hash)> _duplicates = new();

    public DuplicateManager(ModManager modManager, SaveService saveService)
    {
        _modManager  = modManager;
        _saveService = saveService;
    }

    public IReadOnlyList<(FullPath[] Paths, long Size, byte[] Hash)> Duplicates
        => _duplicates;

    public long SavedSpace { get; private set; } = 0;
    public Task Worker     { get; private set; } = Task.CompletedTask;

    private CancellationTokenSource _cancellationTokenSource = new();

    public void StartDuplicateCheck(IEnumerable<FileRegistry> files)
    {
        if (!Worker.IsCompleted)
            return;

        var filesTmp = files.OrderByDescending(f => f.FileSize).ToArray();
        _cancellationTokenSource = new CancellationTokenSource();
        Worker                   = Task.Run(() => CheckDuplicates(filesTmp, _cancellationTokenSource.Token), _cancellationTokenSource.Token);
    }

    public void DeleteDuplicates(ModFileCollection files, Mod mod, ISubMod option, bool useModManager)
    {
        if (!Worker.IsCompleted || _duplicates.Count == 0)
            return;

        foreach (var (set, _, _) in _duplicates)
        {
            if (set.Length < 2)
                continue;

            var remaining = set[0];
            foreach (var duplicate in set.Skip(1))
                HandleDuplicate(mod, duplicate, remaining, useModManager);
        }

        _duplicates.Clear();
        DeleteEmptyDirectories(mod.ModPath);
        files.UpdateAll(mod, option);
    }

    public void Clear()
    {
        _cancellationTokenSource.Cancel();
        Worker     = Task.CompletedTask;
        _duplicates.Clear();
        SavedSpace = 0;
    }

    private void HandleDuplicate(Mod mod, FullPath duplicate, FullPath remaining, bool useModManager)
    {
        void HandleSubMod(ISubMod subMod, int groupIdx, int optionIdx)
        {
            var changes = false;
            var dict = subMod.Files.ToDictionary(kvp => kvp.Key,
                kvp => ChangeDuplicatePath(mod, kvp.Value, duplicate, remaining, kvp.Key, ref changes));
            if (!changes)
                return;

            if (useModManager)
            {
                _modManager.OptionEditor.OptionSetFiles(mod, groupIdx, optionIdx, dict);
            }
            else
            {
                var sub = (SubMod)subMod;
                sub.FileData = dict;
                _saveService.ImmediateSave(new ModSaveGroup(mod, groupIdx));
            }
        }

        ModEditor.ApplyToAllOptions(mod, HandleSubMod);

        try
        {
            File.Delete(duplicate.FullName);
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"[DeleteDuplicates] Could not delete duplicate {duplicate.FullName} of {remaining.FullName}:\n{e}");
        }
    }

    private static FullPath ChangeDuplicatePath(Mod mod, FullPath value, FullPath from, FullPath to, Utf8GamePath key, ref bool changes)
    {
        if (!value.Equals(from))
            return value;

        changes = true;
        Penumbra.Log.Debug($"[DeleteDuplicates] Changing {key} for {mod.Name}\n     : {from}\n    -> {to}");
        return to;
    }

    private void CheckDuplicates(IReadOnlyList<FileRegistry> files, CancellationToken token)
    {
        _duplicates.Clear();
        SavedSpace = 0;
        var list     = new List<FullPath>();
        var lastSize = -1L;
        foreach (var file in files)
        {
            // Skip any UI Files because deduplication causes weird crashes for those.
            if (file.SubModUsage.Any(f => f.Item2.Path.StartsWith("ui/"u8)))
                continue;

            token.ThrowIfCancellationRequested();

            if (file.FileSize == lastSize)
            {
                list.Add(file.File);
                continue;
            }

            if (list.Count >= 2)
                CheckMultiDuplicates(list, lastSize, token);

            lastSize = file.FileSize;

            list.Clear();
            list.Add(file.File);
        }

        if (list.Count >= 2)
            CheckMultiDuplicates(list, lastSize, token);

        _duplicates.Sort((a, b) => a.Size != b.Size ? b.Size.CompareTo(a.Size) : a.Paths[0].CompareTo(b.Paths[0]));
    }

    private void CheckMultiDuplicates(IReadOnlyList<FullPath> list, long size, CancellationToken token)
    {
        var hashes = list.Select(f => (f, ComputeHash(f))).ToList();
        while (hashes.Count > 0)
        {
            token.ThrowIfCancellationRequested();

            var set  = new HashSet<FullPath> { hashes[0].Item1 };
            var hash = hashes[0];
            for (var j = 1; j < hashes.Count; ++j)
            {
                token.ThrowIfCancellationRequested();

                if (CompareHashes(hash.Item2, hashes[j].Item2) && CompareFilesDirectly(hashes[0].Item1, hashes[j].Item1))
                    set.Add(hashes[j].Item1);
            }

            hashes.RemoveAll(p => set.Contains(p.Item1));
            if (set.Count > 1)
            {
                _duplicates.Add((set.OrderBy(f => f.FullName.Length).ToArray(), size, hash.Item2));
                SavedSpace += (set.Count - 1) * size;
            }
        }
    }

    /// <summary> Check if two files are identical on a binary level. Returns true if they are identical. </summary>
    public static unsafe bool CompareFilesDirectly(FullPath f1, FullPath f2)
    {
        if (!f1.Exists || !f2.Exists)
            return false;

        using var s1      = File.OpenRead(f1.FullName);
        using var s2      = File.OpenRead(f2.FullName);
        var       buffer1 = stackalloc byte[256];
        var       buffer2 = stackalloc byte[256];
        var       span1   = new Span<byte>(buffer1, 256);
        var       span2   = new Span<byte>(buffer2, 256);

        while (true)
        {
            var bytes1 = s1.Read(span1);
            var bytes2 = s2.Read(span2);
            if (bytes1 != bytes2)
                return false;

            if (!span1[..bytes1].SequenceEqual(span2[..bytes2]))
                return false;

            if (bytes1 < 256)
                return true;
        }
    }

    public static bool CompareHashes(byte[] f1, byte[] f2)
        => StructuralComparisons.StructuralEqualityComparer.Equals(f1, f2);

    public byte[] ComputeHash(FullPath f)
    {
        using var stream = File.OpenRead(f.FullName);
        return _hasher.ComputeHash(stream);
    }

    /// <summary>
    /// Recursively delete all empty directories starting from the given directory.
    /// Deletes inner directories first, so that a tree of empty directories is actually deleted.
    /// </summary>
    private static void DeleteEmptyDirectories(DirectoryInfo baseDir)
    {
        try
        {
            if (!baseDir.Exists)
                return;

            foreach (var dir in baseDir.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
                DeleteEmptyDirectories(dir);

            baseDir.Refresh();
            if (!baseDir.EnumerateFileSystemInfos().Any())
                Directory.Delete(baseDir.FullName, false);
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not delete empty directories in {baseDir.FullName}:\n{e}");
        }
    }


    /// <summary> Deduplicate a mod simply by its directory without any confirmation or waiting time. </summary>
    internal void DeduplicateMod(DirectoryInfo modDirectory)
    {
        try
        {
            var mod = new Mod(modDirectory);
            _modManager.Creator.ReloadMod(mod, true, out _);

            Clear();
            var files = new ModFileCollection();
            files.UpdateAll(mod, mod.Default);
            CheckDuplicates(files.Available.OrderByDescending(f => f.FileSize).ToArray(), CancellationToken.None);
            DeleteDuplicates(files, mod, mod.Default, false);
        }
        catch (Exception e)
        {
            Penumbra.Log.Warning($"Could not deduplicate mod {modDirectory.Name}:\n{e}");
        }
    }
}
