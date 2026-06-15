using ImSharp;
using Penumbra.Communication;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Services;

namespace Penumbra.UI.ModsTab.Groups;

public sealed class GroupNameCache : BasicCache
{
    private readonly CommunicatorService _communicator;
    private readonly ModSelection        _selection;

    public record PageData(StringPair Name, StringPair DefaultName, int Page, bool CustomName, bool Visible, List<IModGroup> Groups)
    {
        public bool Visible { get; set; } = Visible;
    }

    public readonly SortedList<int, PageData> Pages        = [];
    public readonly List<PageData>            VisiblePages = [];

    public bool ShowPages
        => VisiblePages.Count > 1;

    public GroupNameCache(CommunicatorService communicator, ModSelection selection)
    {
        _communicator = communicator;
        _selection    = selection;
        communicator.ModDataChanged.Subscribe(OnModDataChange, ModDataChanged.Priority.GroupNameCache);
        communicator.ModOptionChanged.Subscribe(OnModOptionChange, ModOptionChanged.Priority.GroupNameCache);
        selection.Subscribe(OnSelectionChange, ModSelection.Priority.GroupNameCache);
    }

    public override void Update()
    {
        if (!CustomDirty)
            return;

        Pages.Clear();
        VisiblePages.Clear();
        if (_selection.Mod is not { } mod)
            return;

        foreach (var (page, name) in mod.PageNames)
            Pages.Add(page, new PageData(new StringPair(name), new StringPair($"Page {page + 1}"), page, true, false, []));

        foreach (var group in mod.Groups)
        {
            if (!Pages.TryGetValue(group.Page, out var data))
            {
                var defaultName = new StringPair($"Page {group.Page + 1}");
                data = new PageData(defaultName, defaultName, group.Page, false, group.IsOption, [group]);
                Pages.Add(group.Page, data);
            }
            else
            {
                data.Groups.Add(group);
                data.Visible |= group.IsOption;
            }
        }

        foreach (var page in Pages.Values.Where(p => p.Visible))
            VisiblePages.Add(page);
    }

    private void OnModOptionChange(in ModOptionChanged.Arguments arguments)
    {
        if (arguments.Mod != _selection.Mod)
            return;

        if (arguments.Type is ModOptionChangeType.GroupAdded or ModOptionChangeType.GroupDeleted or ModOptionChangeType.GroupMoved
            or ModOptionChangeType.GroupRenamed or ModOptionChangeType.DisplayChange)
            Dirty |= IManagedCache.DirtyFlags.Custom;
    }

    private void OnModDataChange(in ModDataChanged.Arguments arguments)
    {
        if (arguments.Type is ModDataChangeType.PageNames && arguments.Mod == _selection.Mod)
            Dirty |= IManagedCache.DirtyFlags.Custom;
    }

    private void OnSelectionChange(in ModSelection.Arguments arguments)
        => Dirty |= IManagedCache.DirtyFlags.Custom;

    protected override void Dispose(bool disposing)
    {
        _communicator.ModDataChanged.Unsubscribe(OnModDataChange);
        _communicator.ModOptionChanged.Unsubscribe(OnModOptionChange);
        _selection.Unsubscribe(OnSelectionChange);
    }
}
