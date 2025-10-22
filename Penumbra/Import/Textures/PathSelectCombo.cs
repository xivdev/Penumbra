using Dalamud.Plugin.Services;
using ImSharp;
using Penumbra.Api.Enums;
using Penumbra.Interop.ResourceTree;
using Penumbra.Mods.Editor;
using Penumbra.UI.Classes;

namespace Penumbra.Import.Textures;

public abstract class PathSelectCombo(IDataManager dataManager) : FilterComboBase<PathSelectCombo.PathData>
{
    public bool Draw(Utf8StringHandler<LabelStringHandlerBuffer> label, Utf8StringHandler<HintStringHandlerBuffer> tooltip, string current,
        int skipPrefix, out string newPath)
    {
        _skipPrefix = skipPrefix;
        _selected   = current;
        if (!base.Draw(label, current.Length > 0 ? current : "Choose a modded texture from this mod here..."u8, tooltip,
                Im.ContentRegion.Available.X, out var ret))
        {
            newPath = string.Empty;
            return false;
        }

        newPath = ret.SearchPath;
        return true;
    }

    public record PathData(StringU8 Path, string SearchPath, bool IsOnPlayer, bool IsGame);
    private int    _skipPrefix;
    private string _selected = string.Empty;

    protected abstract IEnumerable<FileRegistry> GetFiles();
    protected abstract ISet<string>              GetPlayerResources();

    protected override IEnumerable<PathData> GetItems()
    {
        var playerResources = GetPlayerResources();
        var files           = GetFiles();

        foreach (var (file, game) in files.SelectMany(f => f.SubModUsage.Select(p => (p.Item2.ToString(), true))
                         .Prepend((f.File.FullName, false)))
                     .Where(p => p.Item2 ? dataManager.FileExists(p.Item1) : File.Exists(p.Item1)))
        {
            var onPlayer      = playerResources.Contains(file);
            var displayString = game ? new StringU8($"--> {file}") : new StringU8(file.AsSpan(_skipPrefix));
            yield return new PathData(displayString, file, onPlayer, game);
        }
    }

    protected override float ItemHeight
        => Im.Style.TextHeightWithSpacing;

    protected override bool DrawItem(in PathData item, int globalIndex, bool selected)
    {
        var textColor = item.IsOnPlayer ? ColorId.HandledConflictMod.Value() :
            item.IsGame                 ? ColorId.FolderExpanded.Value() : ColorParameter.Default;
        bool ret;
        using (ImGuiColor.Text.Push(textColor))
        {
            ret = Im.Selectable(item.Path, selected);
        }

        Im.Tooltip.OnHover(item.IsGame
            ? "This is a game path and refers to an unmanipulated file from your game data."u8
            : "This is a path to a modded file on your file system."u8);
        return ret;
    }

    protected override bool IsSelected(PathData item, int globalIndex)
        => string.Equals(_selected, item.SearchPath, StringComparison.OrdinalIgnoreCase);
}

public sealed class TextureSelectCombo(ResourceTreeFactory resources, ModEditor editor, IDataManager dataManager) : PathSelectCombo(dataManager)
{
    protected override IEnumerable<FileRegistry> GetFiles()
        => editor.Files.Tex;

    protected override ISet<string> GetPlayerResources()
        => ResourceTreeApiHelper.GetPlayerResourcesOfType(resources, ResourceType.Tex);
}
