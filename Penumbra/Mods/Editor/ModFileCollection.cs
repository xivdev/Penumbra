using OtterGui;
using OtterGui.Services;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;

namespace Penumbra.Mods.Editor;

public class ModFileCollection : IDisposable, IService
{
    private readonly List<FileRegistry> _available = [];
    private readonly List<FileRegistry> _mtrl      = [];
    private readonly List<FileRegistry> _mdl       = [];
    private readonly List<FileRegistry> _tex       = [];
    private readonly List<FileRegistry> _shpk      = [];
    private readonly List<FileRegistry> _pbd       = [];

    private readonly SortedSet<FullPath>   _missing   = [];
    private readonly HashSet<Utf8GamePath> _usedPaths = [];

    public IReadOnlySet<FullPath> Missing
        => Ready ? _missing : [];

    public IReadOnlySet<Utf8GamePath> UsedPaths
        => Ready ? _usedPaths : [];

    public IReadOnlyList<FileRegistry> Available
        => Ready ? _available : [];

    public IReadOnlyList<FileRegistry> Mtrl
        => Ready ? _mtrl : [];

    public IReadOnlyList<FileRegistry> Mdl
        => Ready ? _mdl : [];

    public IReadOnlyList<FileRegistry> Tex
        => Ready ? _tex : [];

    public IReadOnlyList<FileRegistry> Shpk
        => Ready ? _shpk : [];

    public IReadOnlyList<FileRegistry> Pbd
        => Ready ? _pbd : [];

    public bool Ready { get; private set; } = true;

    public void UpdateAll(Mod mod, IModDataContainer option)
    {
        UpdateFiles(mod, new CancellationToken());
        UpdatePaths(mod, option, false, new CancellationToken());
    }

    public void UpdatePaths(Mod mod, IModDataContainer option)
        => UpdatePaths(mod, option, true, new CancellationToken());

    public void Clear()
    {
        ClearFiles();
        ClearPaths(false, new CancellationToken());
    }

    public void Dispose()
        => Clear();

    public void ClearMissingFiles()
        => _missing.Clear();

    public void RemoveUsedPath(IModDataContainer option, FileRegistry? file, Utf8GamePath gamePath)
    {
        _usedPaths.Remove(gamePath);
        if (file != null)
        {
            --file.CurrentUsage;
            file.SubModUsage.RemoveAll(p => p.Item1 == option && p.Item2.Equals(gamePath));
        }
    }

    public void RemoveUsedPath(IModDataContainer option, FullPath file, Utf8GamePath gamePath)
        => RemoveUsedPath(option, _available.FirstOrDefault(f => f.File.Equals(file)), gamePath);

    public void AddUsedPath(IModDataContainer option, FileRegistry? file, Utf8GamePath gamePath)
    {
        _usedPaths.Add(gamePath);
        if (file == null)
            return;

        ++file.CurrentUsage;
        file.SubModUsage.Add((option, gamePath));
    }

    public void AddUsedPath(IModDataContainer option, FullPath file, Utf8GamePath gamePath)
        => AddUsedPath(option, _available.FirstOrDefault(f => f.File.Equals(file)), gamePath);

    public void ChangeUsedPath(FileRegistry file, int pathIdx, Utf8GamePath gamePath)
    {
        var oldPath = file.SubModUsage[pathIdx];
        _usedPaths.Remove(oldPath.Item2);
        if (!gamePath.IsEmpty)
        {
            file.SubModUsage[pathIdx] = (oldPath.Item1, gamePath);
            _usedPaths.Add(gamePath);
        }
        else
        {
            --file.CurrentUsage;
            file.SubModUsage.RemoveAt(pathIdx);
        }
    }

    private void UpdateFiles(Mod mod, CancellationToken tok)
    {
        tok.ThrowIfCancellationRequested();
        ClearFiles();

        foreach (var file in mod.ModPath.EnumerateDirectories().Where(d => !d.IsHidden()).SelectMany(FileExtensions.EnumerateNonHiddenFiles))
        {
            tok.ThrowIfCancellationRequested();
            if (!FileRegistry.FromFile(mod.ModPath, file, out var registry))
                continue;

            _available.Add(registry);
            switch (Path.GetExtension(registry.File.FullName).ToLowerInvariant())
            {
                case ".mtrl":
                    _mtrl.Add(registry);
                    break;
                case ".mdl":
                    _mdl.Add(registry);
                    break;
                case ".tex":
                    _tex.Add(registry);
                    break;
                case ".shpk":
                    _shpk.Add(registry);
                    break;
                case ".pbd":
                    _pbd.Add(registry);
                    break;
            }
        }
    }

    private void ClearFiles()
    {
        _available.Clear();
        _mtrl.Clear();
        _mdl.Clear();
        _tex.Clear();
        _shpk.Clear();
        _pbd.Clear();
    }

    private void ClearPaths(bool clearRegistries, CancellationToken tok)
    {
        if (clearRegistries)
            foreach (var reg in _available)
            {
                tok.ThrowIfCancellationRequested();
                reg.CurrentUsage = 0;
                reg.SubModUsage.Clear();
            }

        _missing.Clear();
        _usedPaths.Clear();
    }

    private void UpdatePaths(Mod mod, IModDataContainer option, bool clearRegistries, CancellationToken tok)
    {
        tok.ThrowIfCancellationRequested();
        ClearPaths(clearRegistries, tok);

        tok.ThrowIfCancellationRequested();

        foreach (var subMod in mod.AllDataContainers)
        {
            foreach (var (gamePath, file) in subMod.Files)
            {
                tok.ThrowIfCancellationRequested();
                if (!file.Exists)
                {
                    _missing.Add(file);
                    if (subMod == option)
                        _usedPaths.Add(gamePath);
                }
                else
                {
                    var registry = _available.Find(x => x.File.Equals(file));
                    if (registry == null)
                        continue;

                    if (subMod == option)
                    {
                        ++registry.CurrentUsage;
                        _usedPaths.Add(gamePath);
                    }

                    registry.SubModUsage.Add((subMod, gamePath));
                }
            }
        }
    }
}
