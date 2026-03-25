using Luna;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;

namespace Penumbra.UI.ManagementTab;

public interface IScannedObject
{
    public string FilePath { get; }

    public bool DataPredicate();
}

public class BaseScannedFile(string filePath, Mod mod) : IScannedObject
{
    public string             FilePath     { get; } = filePath;
    public string             RelativePath { get; } = Path.GetRelativePath(mod.ModPath.FullName, filePath);
    public WeakReference<Mod> Mod          { get; } = new(mod);

    public virtual bool DataPredicate()
        => true;
}

public class BaseScannedRedirection(Utf8GamePath path, FullPath redirection, IModDataContainer container, bool swap) : IScannedObject
{
    public Utf8GamePath                     GamePath    { get; } = path;
    public FullPath                         Redirection { get; } = redirection;
    public WeakReference<IModDataContainer> Container   { get; } = new(container);
    public bool                             FileSwap    { get; } = swap;

    public virtual bool DataPredicate()
        => true;

    public string FilePath
        => Redirection.FullName;
}

public abstract class RedirectionScanner<T>(ModManager mods) : ObjectScanner<T>(mods)
    where T : BaseScannedRedirection
{
    protected virtual bool DoCreateRedirection(Utf8GamePath path, FullPath redirection, IModDataContainer container, bool swap)
        => true;

    protected abstract T Create(Utf8GamePath path, FullPath redirection, IModDataContainer container, bool swap);

    public override Task Scan()
    {
        StableList.Clear();
        Cancel();
        var mods  = Mods.ToArray();
        var token = CancelSource.Token;
        ScanTask = Task.Run(() =>
        {
            CurrentProgress = 0;
            ProgressMax     = mods.Length;
            Cache.Clear();
            foreach (var mod in mods)
            {
                try
                {
                    foreach (var container in mod.AllDataContainers)
                    {
                        foreach (var ((gamePath, redirection), swap) in container.Files.Select(kvp => (kvp, false))
                                     .Concat(container.FileSwaps.Select(kvp => (kvp, true))))
                        {
                            if (!DoCreateRedirection(gamePath, redirection, container, swap))
                                continue;

                            if (token.IsCancellationRequested)
                                return;

                            var stored = Create(gamePath, redirection, container, swap);
                            if (stored.DataPredicate())
                                Cache.Enqueue(stored);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error($"Error while scanning redirections for {mod.Name}:\n{ex}");
                }

                ++CurrentProgress;
            }
        }, token);
        return ScanTask;
    }
}

public abstract class ModFileScanner<T>(ModManager mods) : ObjectScanner<T>(mods)
    where T : BaseScannedFile
{
    protected virtual bool DoCreateFile(string fileName, Mod mod)
        => true;

    protected abstract T Create(string fileName, Mod mod);

    public override Task Scan()
    {
        StableList.Clear();
        Cancel();
        var mods  = Mods.ToArray();
        var token = CancelSource.Token;
        ScanTask = Task.Run(() =>
        {
            CurrentProgress = 0;
            ProgressMax     = mods.Length;
            Cache.Clear();
            foreach (var mod in mods)
            {
                try
                {
                    foreach (var directory in mod.ModPath.EnumerateDirectories())
                    {
                        foreach (var file in directory.EnumerateNonHiddenFiles().Where(file => DoCreateFile(file.FullName, mod)))
                        {
                            if (token.IsCancellationRequested)
                                return;

                            var stored = Create(file.FullName, mod);
                            if (stored.DataPredicate())
                                Cache.Enqueue(stored);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error($"Error while scanning mod files for {mod.Name}:\n{ex}");
                }

                ++CurrentProgress;
            }
        }, token);
        return ScanTask;
    }
}

public abstract class ObjectScanner<T>(ModManager mods) : ObjectScanner, IDisposable
    where T : IScannedObject
{
    protected readonly ModManager              Mods = mods;
    protected          Task?                   ScanTask;
    protected          CancellationTokenSource CancelSource = new();

    protected readonly ConcurrentQueue<T> Cache      = [];
    protected readonly List<T>            StableList = [];

    protected int ProgressMax;
    protected int CurrentProgress;

    public sealed override float Progress
        => ProgressMax is 0 ? 1f : CurrentProgress / (float)ProgressMax;

    public sealed override bool Completed
        => ScanTask?.IsCompletedSuccessfully ?? false;

    public sealed override bool Running
        => !ScanTask?.IsCompleted ?? false;

    public IReadOnlyList<T> GetCurrentList()
    {
        while (Cache.TryDequeue(out var redirection))
            StableList.Add(redirection);
        return StableList;
    }

    public IEnumerable<T> GetNewItems()
    {
        while (Cache.TryDequeue(out var redirection))
        {
            StableList.Add(redirection);
            yield return redirection;
        }
    }

    public override void Cancel()
    {
        if (ScanTask is null)
            return;

        ScanTask = null;
        CancelSource.Cancel();
        CancelSource = new CancellationTokenSource();
    }

    public virtual bool RemoveObject(T? toRemove)
    {
        if (toRemove is null)
            return false;

        return StableList.Remove(toRemove);
    }

    public sealed override bool RemoveObject(IScannedObject? toRemove)
    {
        if (toRemove is not T obj)
            return false;

        return RemoveObject(obj);
    }

    public void Dispose()
    {
        CancelSource.Cancel();
        ScanTask = null;
    }
}

public abstract class ObjectScanner
{
    public abstract bool  RemoveObject(IScannedObject? toRemove);
    public abstract Task  Scan();
    public abstract void  Cancel();
    public abstract float Progress  { get; }
    public abstract bool  Completed { get; }
    public abstract bool  Running   { get; }
}
