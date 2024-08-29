using OtterGui.Compression;
using OtterGui.Services;
using Penumbra.Mods.Groups;
using Penumbra.Mods.SubMods;

namespace Penumbra.Mods.Editor;

public class ModEditor(
    ModNormalizer modNormalizer,
    ModMetaEditor metaEditor,
    ModFileCollection files,
    ModFileEditor fileEditor,
    DuplicateManager duplicates,
    ModSwapEditor swapEditor,
    MdlMaterialEditor mdlMaterialEditor,
    FileCompactor compactor)
    : IDisposable, IService
{
    public readonly ModNormalizer     ModNormalizer     = modNormalizer;
    public readonly ModMetaEditor     MetaEditor        = metaEditor;
    public readonly ModFileEditor     FileEditor        = fileEditor;
    public readonly DuplicateManager  Duplicates        = duplicates;
    public readonly ModFileCollection Files             = files;
    public readonly ModSwapEditor     SwapEditor        = swapEditor;
    public readonly MdlMaterialEditor MdlMaterialEditor = mdlMaterialEditor;
    public readonly FileCompactor     Compactor         = compactor;


    public bool IsLoading
    {
        get
        {
            lock (_lock)
            {
                return _loadingMod is { IsCompleted: false };
            }
        }
    }

    private readonly object _lock = new();
    private          Task?  _loadingMod;

    public Mod? Mod      { get; private set; }
    public int  GroupIdx { get; private set; }
    public int  DataIdx  { get; private set; }

    public IModGroup?         Group  { get; private set; }
    public IModDataContainer? Option { get; private set; }

    public async Task LoadMod(Mod mod, int groupIdx, int dataIdx)
    {
        await AppendTask(() =>
        {
            Mod = mod;
            LoadOption(groupIdx, dataIdx, true);
            Files.UpdateAll(mod, Option!);
            SwapEditor.Revert(Option!);
            MetaEditor.Load(Mod!, Option!);
            Duplicates.Clear();
            MdlMaterialEditor.ScanModels(Mod!);
        });
    }

    private Task AppendTask(Action run)
    {
        lock (_lock)
        {
            if (_loadingMod == null || _loadingMod.IsCompleted)
                return _loadingMod = Task.Run(run);

            return _loadingMod = _loadingMod.ContinueWith(_ => run());
        }
    }

    public async Task LoadOption(int groupIdx, int dataIdx)
    {
        await AppendTask(() =>
        {
            LoadOption(groupIdx, dataIdx, true);
            SwapEditor.Revert(Option!);
            Files.UpdatePaths(Mod!, Option!);
            MetaEditor.Load(Mod!, Option!);
            FileEditor.Clear();
            Duplicates.Clear();
        });
    }

    /// <summary> Load the correct option by indices for the currently loaded mod if possible, unload if not.  </summary>
    private void LoadOption(int groupIdx, int dataIdx, bool message)
    {
        if (Mod != null && Mod.Groups.Count > groupIdx)
        {
            if (groupIdx == -1 && dataIdx == 0)
            {
                Group    = null;
                Option   = Mod.Default;
                GroupIdx = groupIdx;
                DataIdx  = dataIdx;
                return;
            }

            if (groupIdx >= 0)
            {
                Group = Mod.Groups[groupIdx];
                if (dataIdx >= 0 && dataIdx < Group.DataContainers.Count)
                {
                    Option   = Group.DataContainers[dataIdx];
                    GroupIdx = groupIdx;
                    DataIdx  = dataIdx;
                    return;
                }
            }
        }

        Group    = null;
        Option   = Mod?.Default;
        GroupIdx = -1;
        DataIdx  = 0;
        if (message)
            Penumbra.Log.Error($"Loading invalid option {groupIdx} {dataIdx} for Mod {Mod?.Name ?? "Unknown"}.");
    }

    public void Clear()
    {
        Duplicates.Clear();
        FileEditor.Clear();
        Files.Clear();
        MetaEditor.Clear();
        Mod = null;
        LoadOption(0, 0, false);
    }

    public void Dispose()
        => Clear();

    /// <summary> Apply a option action to all available option in a mod, including the default option. </summary>
    public static void ApplyToAllContainers(Mod mod, Action<IModDataContainer> action)
    {
        action(mod.Default);
        foreach (var container in mod.Groups.SelectMany(g => g.DataContainers))
            action(container);
    }

    // Does not delete the base directory itself even if it is completely empty at the end.
    public static void ClearEmptySubDirectories(DirectoryInfo baseDir)
    {
        foreach (var subDir in baseDir.GetDirectories())
        {
            ClearEmptySubDirectories(subDir);
            if (subDir.GetFiles().Length == 0 && subDir.GetDirectories().Length == 0)
                subDir.Delete();
        }
    }
}
