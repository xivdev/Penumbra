using ImSharp.Containers;
using Luna;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;

namespace Penumbra.Mods.Editor;

public class ModFileCollection : IDisposable
{
    private readonly ObservableList<FileRegistry> _available = [];
    private readonly ObservableList<FileRegistry> _mtrl      = [];
    private readonly ObservableList<FileRegistry> _mdl       = [];
    private readonly ObservableList<FileRegistry> _tex       = [];
    private readonly ObservableList<FileRegistry> _shpk      = [];
    private readonly ObservableList<FileRegistry> _pbd       = [];
    private readonly ObservableList<FileRegistry> _atch      = [];

    private readonly SortedSet<FullPath>   _missing   = [];
    private readonly HashSet<Utf8GamePath> _usedPaths = [];

    public IReadOnlySet<FullPath> Missing
        => Ready ? _missing : [];

    public IReadOnlySet<Utf8GamePath> UsedPaths
        => Ready ? _usedPaths : [];

    public IObservableList<FileRegistry> Available
        => Ready ? _available : [];

    public IObservableList<FileRegistry> Mtrl
        => Ready ? _mtrl : [];

    public IObservableList<FileRegistry> Mdl
        => Ready ? _mdl : [];

    public IObservableList<FileRegistry> Tex
        => Ready ? _tex : [];

    public IObservableList<FileRegistry> Shpk
        => Ready ? _shpk : [];

    public IObservableList<FileRegistry> Pbd
        => Ready ? _pbd : [];

    public IObservableList<FileRegistry> Atch
        => Ready ? _atch : [];

    public bool Ready { get; private set; } = true;

    public void UpdateAll(Mod mod, IModDataContainer option)
    {
        UpdateFiles(mod, CancellationToken.None);
        UpdatePaths(mod, option, false, CancellationToken.None);
    }

    public void UpdatePaths(Mod mod, IModDataContainer option)
        => UpdatePaths(mod, option, true, CancellationToken.None);

    public void Clear()
    {
        ClearFiles();
        ClearPaths(false, CancellationToken.None);
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
                case ".mtrl": _mtrl.Add(registry); break;
                case ".mdl":  _mdl.Add(registry); break;
                case ".tex":  _tex.Add(registry); break;
                case ".shpk": _shpk.Add(registry); break;
                case ".pbd":  _pbd.Add(registry); break;
                case ".atch": _atch.Add(registry); break;
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
        _atch.Clear();
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
