using System;
using System.Collections.Generic;
using System.Linq;
using OtterGui;
using OtterGui.Classes;
using Penumbra.Import;
using Penumbra.Meta;
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

    // Access
    public override string ToString()
        => Name.Text;

    public void WriteAllTexToolsMeta(MetaFileManager manager)
    {
        try
        {
            TexToolsMeta.WriteTexToolsMeta(manager, Default.Manipulations, ModPath);
            foreach (var group in Groups)
            {
                var dir = ModCreator.NewOptionDirectory(ModPath, group.Name);
                if (!dir.Exists)
                    dir.Create();

                foreach (var option in group.OfType<SubMod>())
                {
                    var optionDir = ModCreator.NewOptionDirectory(dir, option.Name);
                    if (!optionDir.Exists)
                        optionDir.Create();

                    TexToolsMeta.WriteTexToolsMeta(manager, option.Manipulations, optionDir);
                }
            }
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Error writing TexToolsMeta:\n{e}");
        }
    }

}
