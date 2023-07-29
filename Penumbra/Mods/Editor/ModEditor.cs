using System;
using System.IO;
using OtterGui;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Subclasses;

namespace Penumbra.Mods;

public class ModEditor : IDisposable
{
    public readonly ModNormalizer     ModNormalizer;
    public readonly ModMetaEditor     MetaEditor;
    public readonly ModFileEditor     FileEditor;
    public readonly DuplicateManager  Duplicates;
    public readonly ModFileCollection Files;
    public readonly ModSwapEditor     SwapEditor;
    public readonly MdlMaterialEditor MdlMaterialEditor;

    public Mod? Mod       { get; private set; }
    public int  GroupIdx  { get; private set; }
    public int  OptionIdx { get; private set; }

    public IModGroup? Group  { get; private set; }
    public ISubMod?   Option { get; private set; }

    public ModEditor(ModNormalizer modNormalizer, ModMetaEditor metaEditor, ModFileCollection files,
        ModFileEditor fileEditor, DuplicateManager duplicates, ModSwapEditor swapEditor, MdlMaterialEditor mdlMaterialEditor)
    {
        ModNormalizer     = modNormalizer;
        MetaEditor        = metaEditor;
        Files             = files;
        FileEditor        = fileEditor;
        Duplicates        = duplicates;
        SwapEditor        = swapEditor;
        MdlMaterialEditor = mdlMaterialEditor;
    }

    public void LoadMod(Mod mod)
        => LoadMod(mod, -1, 0);

    public void LoadMod(Mod mod, int groupIdx, int optionIdx)
    {
        Mod = mod;
        LoadOption(groupIdx, optionIdx, true);
        Files.UpdateAll(mod, Option!);
        SwapEditor.Revert(Option!);
        MetaEditor.Load(Mod!, Option!);
        Duplicates.Clear();
        MdlMaterialEditor.ScanModels(Mod!);
    }

    public void LoadOption(int groupIdx, int optionIdx)
    {
        LoadOption(groupIdx, optionIdx, true);
        SwapEditor.Revert(Option!);
        Files.UpdatePaths(Mod!, Option!);
        MetaEditor.Load(Mod!, Option!);
        FileEditor.Clear();
        Duplicates.Clear();
    }

    /// <summary> Load the correct option by indices for the currently loaded mod if possible, unload if not.  </summary>
    private void LoadOption(int groupIdx, int optionIdx, bool message)
    {
        if (Mod != null && Mod.Groups.Count > groupIdx)
        {
            if (groupIdx == -1 && optionIdx == 0)
            {
                Group     = null;
                Option    = Mod.Default;
                GroupIdx  = groupIdx;
                OptionIdx = optionIdx;
                return;
            }

            if (groupIdx >= 0)
            {
                Group = Mod.Groups[groupIdx];
                if (optionIdx >= 0 && optionIdx < Group.Count)
                {
                    Option    = Group[optionIdx];
                    GroupIdx  = groupIdx;
                    OptionIdx = optionIdx;
                    return;
                }
            }
        }

        Group     = null;
        Option    = Mod?.Default;
        GroupIdx  = -1;
        OptionIdx = 0;
        if (message)
            Penumbra.Log.Error($"Loading invalid option {groupIdx} {optionIdx} for Mod {Mod?.Name ?? "Unknown"}.");
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
    public static void ApplyToAllOptions(Mod mod, Action<ISubMod, int, int> action)
    {
        action(mod.Default, -1, 0);
        foreach (var (group, groupIdx) in mod.Groups.WithIndex())
        {
            for (var optionIdx = 0; optionIdx < group.Count; ++optionIdx)
                action(group[optionIdx], groupIdx, optionIdx);
        }
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
