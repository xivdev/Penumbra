using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OtterGui;
using OtterGui.Classes;
using Penumbra.Collections.Cache;
using Penumbra.Import;
using Penumbra.Meta;
using Penumbra.Mods.Subclasses;
using Penumbra.String.Classes;

namespace Penumbra.Mods;

public sealed partial class Mod : IMod
{
    public static readonly TemporaryMod ForcedFiles = new()
    {
        Name     = "Forced Files",
        Index    = -1,
        Priority = int.MaxValue,
    };

    // Main Data
    public DirectoryInfo ModPath { get; internal set; }

    public string Identifier
        => Index >= 0 ? ModPath.Name : Name;

    public int Index { get; internal set; } = -1;

    public bool IsTemporary
        => Index < 0;

    /// <summary>Unused if Index < 0 but used for special temporary mods.</summary>
    public int Priority
        => 0;

    internal Mod(DirectoryInfo modPath)
    {
        ModPath = modPath;
        Default = new SubMod(this);
    }

    public override string ToString()
        => Name.Text;

    // Meta Data
    public LowerString           Name        { get; internal set; } = "New Mod";
    public LowerString           Author      { get; internal set; } = LowerString.Empty;
    public string                Description { get; internal set; } = string.Empty;
    public string                Version     { get; internal set; } = string.Empty;
    public string                Website     { get; internal set; } = string.Empty;
    public IReadOnlyList<string> ModTags     { get; internal set; } = Array.Empty<string>();


    // Local Data
    public long                  ImportDate { get; internal set; } = DateTimeOffset.UnixEpoch.ToUnixTimeMilliseconds();
    public IReadOnlyList<string> LocalTags  { get; internal set; } = Array.Empty<string>();
    public string                Note       { get; internal set; } = string.Empty;
    public bool                  Favorite   { get; internal set; } = false;


    // Options
    public readonly SubMod          Default;
    public readonly List<IModGroup> Groups = new();

    ISubMod IMod.Default
        => Default;

    IReadOnlyList<IModGroup> IMod.Groups
        => Groups;

    public IEnumerable<SubMod> AllSubMods
        => Groups.SelectMany(o => o).OfType<SubMod>().Prepend(Default);

    public List<FullPath> FindUnusedFiles()
    {
        var modFiles = AllSubMods.SelectMany(o => o.Files)
            .Select(p => p.Value)
            .ToHashSet();
        return ModPath.EnumerateDirectories()
            .Where(d => !d.IsHidden())
            .SelectMany(FileExtensions.EnumerateNonHiddenFiles)
            .Select(f => new FullPath(f))
            .Where(f => !modFiles.Contains(f))
            .ToList();
    }

    // Cache
    public readonly IReadOnlyDictionary<string, object?> ChangedItems = new SortedList<string, object?>();

    public string LowerChangedItemsString { get; internal set; } = string.Empty;
    public string AllTagsLower            { get; internal set; } = string.Empty;

    public int  TotalFileCount     { get; internal set; }
    public int  TotalSwapCount     { get; internal set; }
    public int  TotalManipulations { get; internal set; }
    public bool HasOptions         { get; internal set; }
}
