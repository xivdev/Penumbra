using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.SubMods;
using Penumbra.Services;

namespace Penumbra.UI.ModsTab.Settings;

public sealed class ModSettingsCache : BasicCache
{
    private readonly        ModSelection        _selection;
    private readonly        UiConfig            _config;
    private readonly        CommunicatorService _communicator;
    private readonly unsafe Im.Native.Storage*  _storage;

    public unsafe Im.StateStorage Storage
        => _storage;

    public readonly  List<ModSettingPage>                 VisiblePages   = [];
    private readonly Dictionary<Guid, ModSettingDataNode> _nodes         = [];
    private readonly List<ModSettingGroup>                _orderedGroups = [];
    private readonly Dictionary<int, ModSettingPage>      _pages         = [];

    public Vector2 ScaledSpacing;
    public float   WidestLabel;
    public float   WidestCombo;
    public float   Height;
    public float   HalfHeight;
    public float   LineWidth;
    public float   BorderWidth;
    public float   TextAlignment;
    public float   ComboAlignment;
    public float   CenterSpacing;
    public float   Indentation;
    public float   CaretTipSpacing;
    public float   CaretWidth;
    public float   HelpIconSize;
    public bool    AnyConditions;
    public bool    DisplayDirty = true;
    public bool    DrawDirty    = true;

    public unsafe ModSettingsCache(ModSelection selection, UiConfig config, CommunicatorService communicator, Im.StateStorage storage)
    {
        _selection    = selection;
        _config       = config;
        _communicator = communicator;
        _storage      = storage.Pointer;
        _selection.Subscribe(OnSelectionChanged, ModSelection.Priority.ModPanel);
        _communicator.ModOptionChanged.Subscribe(OnModOptionChanged, ModOptionChanged.Priority.ModGroupCache);
        _communicator.ModSettingChanged.Subscribe(OnModSettingChanged, ModSettingChanged.Priority.ModGroupCache);
        _communicator.ModPathChanged.Subscribe(OnModPathChanged, ModPathChanged.Priority.ModGroupCache);
    }

    /// <summary>
    ///   Structure is:
    ///     - Data is set dirty and subsequently updated when anything in the mod or option or config changes or the cache is not drawn for a frame.
    ///     - Display is set dirty and subsequently updated when the settings for the mod change (or data was updated).
    ///     - Draw is set dirty and subsequently updated when the collapsible trees are toggled (or display was updated).
    /// </summary>
    public override void Update()
    {
        UpdateData();
        UpdateDisplay();
        UpdateDraw();
    }

    #region Update Draw

    private void UpdateDraw()
    {
        if (!DrawDirty)
            return;

        DrawDirty = false;
        // With no visible pages, there is nothing to do.
        if (VisiblePages.Count is 0)
            return;

        // Set the intended spacing to compute correct height values.
        using var style = ImStyleDouble.ItemSpacing.Push(ScaledSpacing);
        foreach (var page in VisiblePages)
            AddPage(page);

        // Clear trailing spaces
        foreach (var page in VisiblePages)
        {
            while (page.Drawing.Count > 0 && page.Drawing.Last().DrawMode is ModSettingDrawNode.Mode.Space)
                page.Drawing.RemoveAt(page.Drawing.Count - 1);
        }
    }

    private void AddPage(ModSettingPage page)
    {
        using var idStack = Im.Id.Push(page.Id);
        page.Drawing.Clear();
        page.VerticalLines.Clear();

        var (list, lines) = _config.DisplayPages
            ? (page.Drawing, page.VerticalLines)
            : (VisiblePages[0].Drawing, VisiblePages[0].VerticalLines);

        var id               = Im.Id.Get("Page"u8);
        var currentIndex     = _config.DisplayPages ? -1 : list.Count;
        var parentLineOffset = _config.DisplayPages || VisiblePages.Count <= 1 ? 0 : CaretTipSpacing;
        var expanded         = Storage.GetBool(id, true);

        // If the user displays pages as headers, add a header line.
        if (!_config.DisplayPages && VisiblePages.Count > 1)
            list.Add(new ModSettingDrawNode
            {
                Id                = id,
                DrawMode          = ModSettingDrawNode.Mode.PageHeader,
                Node              = page,
                Collapsible       = true,
                Expanded          = expanded,
                Indent            = 0,
                IncomingLineWidth = 0,
            });

        if (!expanded)
            return;

        // Add children and parent line.
        var lastIndex = -1;
        foreach (var group in page.VisibleGroups)
            lastIndex = AddGroup(list, lines, group, currentIndex, 0, parentLineOffset) ?? lastIndex;
        AddSpace(page.Drawing);

        if (!_config.DisplayPages)
            AddOutgoingLine(lines, CaretTipSpacing, lastIndex, currentIndex);
    }

    private int? AddGroup(List<ModSettingDrawNode> list, List<ModSettingsDrawLine> lines, ModSettingGroup group, int parentIndex,
        float parentIndent, float parentLineOffset)
    {
        using var idStack      = Im.Id.Push(group.Group.Index);
        var       id           = Im.Id.Current;
        int?      currentIndex = null;
        var       lastIndex    = -1;
        var       expanded     = group.Collapsible && Storage.GetBool(id, !group.Group.Layout.HasFlag(ModSettingsLayout.DefaultClosed));
        var       indent       = parentIndent;

        // Groups that are not actually drawn as groups and only group their settings require special consideration.
        // Hidden header labels further complicate that.
        var hideHeader = group.HideHeader && parentIndex >= 0 && list[parentIndex].DrawMode is not ModSettingDrawNode.Mode.PageHeader;
        var drawMode = group.IsCombo
            ? hideHeader ? ModSettingDrawNode.Mode.Combo : ModSettingDrawNode.Mode.ComboLabel
            : group.IsSameLineOption
                ? hideHeader ? ModSettingDrawNode.Mode.Checkbox : ModSettingDrawNode.Mode.CheckboxLabel
                : ModSettingDrawNode.Mode.Label;

        var parentOfChildrenIndex = parentIndex;
        var skippedHeader         = drawMode is ModSettingDrawNode.Mode.Label && hideHeader;
        if (!skippedHeader)
        {
            // One less indentation.
            var labelWidth = Math.Max(group.LabelExtend, WidestLabel) - indent;
            indent                += Indentation;
            currentIndex          =  list.Count;
            parentOfChildrenIndex =  currentIndex.Value;
            (labelWidth, var comboWidth, var secondItemOffset) = drawMode switch
            {
                ModSettingDrawNode.Mode.Label => (labelWidth, 0f, indent + labelWidth + CenterSpacing),
                ModSettingDrawNode.Mode.CheckboxLabel or ModSettingDrawNode.Mode.ComboLabel => (labelWidth,
                    MathF.Max(group.ComboWidth, WidestCombo),
                    indent + labelWidth + CenterSpacing),
                ModSettingDrawNode.Mode.Checkbox or ModSettingDrawNode.Mode.Combo => (0, MathF.Max(group.ComboWidth, WidestCombo),
                    currentIndex is 0 ? 0 : list[parentIndex].SecondItemOffset),
                _ => throw new Exception("Can not happen."),
            };

            (var incomingLine, indent) = drawMode is ModSettingDrawNode.Mode.Checkbox or ModSettingDrawNode.Mode.Combo
                ? (secondItemOffset - parentIndent - parentLineOffset, secondItemOffset)
                : (indent - parentIndent - parentLineOffset, indent);

            var node = new ModSettingDrawNode
            {
                Id                = id,
                DrawMode          = drawMode,
                Node              = group,
                Collapsible       = group.Collapsible,
                Expanded          = expanded,
                Indent            = indent,
                IncomingLineWidth = incomingLine,
                LabelWidth        = new Vector2(labelWidth, Height),
                ComboWidth        = new Vector2(comboWidth, Height),
                SecondItemOffset  = secondItemOffset,
            };
            list.Add(node);
        }

        if (expanded)
        {
            var lineOffset = skippedHeader ? HalfHeight : CaretTipSpacing;
            foreach (var child in group.VisibleChildren)
            {
                if (child is ModSettingGroup childGroup)
                    lastIndex = AddGroup(list, lines, childGroup, parentOfChildrenIndex, indent, lineOffset) ?? lastIndex;
                else if (group is { IsCombo: false, IsSameLineOption: false })
                    lastIndex = AddOption(list, lines, (ModSettingOption)child, indent, lineOffset);
            }

            if (currentIndex.HasValue)
                AddOutgoingLine(lines, indent + CaretTipSpacing, lastIndex, currentIndex.Value);
        }

        if (group.Space)
            AddSpace(list);

        return hideHeader && lastIndex >= 0 ? lastIndex : currentIndex;
    }

    private int AddOption(List<ModSettingDrawNode> list, List<ModSettingsDrawLine> lines, ModSettingOption option, float parentIndent,
        float parentLineOffset)
    {
        using var idStack      = Im.Id.Push(option.Data.GroupIndex).Push(option.Data.Index);
        var       id           = Im.Id.Current;
        var       currentIndex = list.Count;
        var       lastIndex    = -1;
        var       indent       = parentIndent + Indentation;

        var (drawMode, line) = option.Radio
            ? (ModSettingDrawNode.Mode.RadioButton, indent - parentIndent - parentLineOffset)
            : (ModSettingDrawNode.Mode.Checkbox, indent - parentIndent - parentLineOffset);

        list.Add(new ModSettingDrawNode
        {
            Id                = id,
            DrawMode          = drawMode,
            Node              = option,
            Collapsible       = false,
            Expanded          = true,
            Indent            = indent,
            IncomingLineWidth = line,
            SecondItemOffset  = indent + Indentation,
        });

        foreach (var group in option.VisibleChildren)
            lastIndex = AddGroup(list, lines, group, currentIndex, indent, HalfHeight) ?? lastIndex;

        AddOutgoingLine(lines, indent + HalfHeight, lastIndex, currentIndex);
        if (option.Space)
            AddSpace(list);

        return currentIndex;
    }

    private void AddOutgoingLine(List<ModSettingsDrawLine> lines, float x, int lastIndex, int currentIndex)
    {
        var withSpacing = Height + ScaledSpacing.Y;
        var length      = (lastIndex - currentIndex) * withSpacing - HalfHeight;
        if (length <= 0)
            return;

        var startPos = (currentIndex + 1) * withSpacing - ScaledSpacing.Y;
        var line     = new ModSettingsDrawLine(x, startPos, length);
        lines.Add(line);
    }

    private static void AddSpace(List<ModSettingDrawNode> nodes)
    {
        if (nodes.Count is 0)
            return;

        var lastIndex = nodes.Count - 1;
        if (nodes[lastIndex].DrawMode is not ModSettingDrawNode.Mode.Space)
            nodes.Add(ModSettingDrawNode.Space);
    }

    #endregion

    #region Update Display

    private void UpdateDisplay()
    {
        if (!DisplayDirty)
            return;

        DisplayDirty = false;
        if (_selection.Mod is not { } mod)
            return;

        var context = new ModSettingContext(mod, _selection.Settings);
        WidestLabel = 0;
        WidestCombo = 0;
        UpdateVisibilities(context);
        if (WidestLabel > _config.ModSettingMaximumExtendLabelWidth * Im.Style.GlobalScale)
            WidestLabel = _config.ModSettingMaximumExtendLabelWidth * Im.Style.GlobalScale;
        DrawDirty = true;
    }

    private void UpdateVisibilities(ModSettingContext context)
    {
        // Check the implicit conditional visibility of all nodes.
        foreach (var node in _nodes.Values)
        {
            IModObject obj = node is ModSettingGroup c ? c.Group : ((ModSettingOption)node).Data;
            AnyConditions |= obj.Condition is not null;
            var condition = obj.Condition is null || obj.Condition.Evaluate(context);
            node.Visible  = condition || !obj.Layout.HasFlag(ModSettingsLayout.Hide) || obj is SingleSubMod;
            node.Disabled = node.Visible && (!condition || obj is SingleModGroup { IsOption: false });
        }

        // Check group visibility by available children.
        VisiblePages.Clear();
        foreach (var page in _pages.Values)
        {
            page.VisibleGroups.Clear();
            foreach (var group in page.Groups)
            {
                if (CheckGroupVisibility(group, 0))
                    page.VisibleGroups.Add(group);
            }

            if (page.VisibleGroups.Count > 0)
                VisiblePages.Add(page);
        }
    }

    private bool CheckGroupVisibility(ModSettingGroup group, int depth)
    {
        if (!group.Visible)
            return false;

        // Set all visible options as visible children.
        group.Depth = depth;
        group.VisibleChildren.Clear();
        foreach (var option in group.AllOptions.Where(o => o.Visible))
        {
            option.Disabled |= group.Disabled;
            group.VisibleChildren.Add(option);
            option.VisibleChildren.Clear();
            // Check visibility of child groups in options.
            foreach (var childGroup in option.Children)
            {
                if (CheckGroupVisibility(childGroup, depth + 2))
                    option.VisibleChildren.Add(childGroup);
            }

            option.HasHiddenChildren = option.VisibleChildren.Count < option.Children.Count;
        }

        group.NumOptions = group.VisibleChildren.Count;

        // Recursively check all child groups of the group itself and add visible ones.
        foreach (var childGroup in group.GroupChildren)
        {
            if (CheckGroupVisibility(childGroup, depth + 1))
                group.VisibleChildren.Add(childGroup);
        }

        switch (group.Behaviour)
        {
            case GroupDrawBehaviour.MultiSelection:
            {
                // Multi Groups with just a single option without group children can be moved to a same line checkbox.
                if (group.NumOptions is 1)
                {
                    var option = (ModSettingOption)group.VisibleChildren[0];
                    if (option.Children.Count is 0)
                        group.IsSameLineOption = true;
                }

                // Multi groups are visible if they have any children at all.
                group.Visible = group.VisibleChildren.Count > 0;
                // Multi groups are collapsible if they are not same-line or have additional group children.
                group.Collapsible = !group.IsSameLineOption || group.VisibleChildren.Count > 1;
                break;
            }
            case GroupDrawBehaviour.SingleSelection:
            {
                // In case of combo, we need to add the group children of all options in order to the group itself.
                // Group children of options have been checked for visibility before.
                if (group.IsCombo)
                {
                    group.ComboWidth = 100 * Im.Style.GlobalScale;
                    for (var i = 0; i < group.NumOptions; ++i)
                    {
                        var option = (ModSettingOption)group.VisibleChildren[i];
                        group.VisibleChildren.AddRange(option.Children.Where(g => g.Visible));
                        if (option.Width > group.ComboWidth)
                            group.ComboWidth = option.Width;
                    }

                    group.ComboWidth += Height + 2 * Im.Style.FramePadding.X;
                }

                // A single group is collapsible if it is not a combo or if it has any group children
                // If it has no options or children itself, collapsibility is irrelevant due to visibility.
                group.Collapsible = !group.IsCombo || group.VisibleChildren.Count > group.NumOptions;
                // A single group is visible if it has at least 2 options or group children.
                group.Visible = group.NumOptions > 1 || group.VisibleChildren.Count > group.NumOptions;
                break;
            }
            default: return false;
        }

        group.LabelWidth  = group.Collapsible ? group.NameWidth + CaretWidth : group.NameWidth;
        group.LabelExtend = group.LabelWidth + group.Depth * Indentation;
        if (group.LabelExtend > WidestLabel && !group.HideHeader)
            WidestLabel = group.LabelExtend;
        if (group.ComboWidth > WidestCombo)
            WidestCombo = group.ComboWidth;
        group.HasHiddenChildren = group.VisibleChildren.Count < group.GroupChildren.Count + group.AllOptions.Count;
        return group.Visible;
    }

    #endregion

    #region Update Data

    private void UpdateData()
    {
        if (!AnyDirty)
            return;

        Reset();
        if (_selection.Mod is not { } mod)
            return;

        foreach (var group in mod.Groups)
        {
            CreateGroupCache(group);
            SetupPage(mod, group.Page);
        }

        UpdateParentage();
    }

    private void Reset()
    {
        // Reset all style data and clear data lists.
        AnyConditions = false;
        Dirty         = IManagedCache.DirtyFlags.Clean;
        _nodes.Clear();
        _pages.Clear();
        _orderedGroups.Clear();
        ScaledSpacing   =  Im.Style.ItemSpacing;
        ScaledSpacing.Y *= _config.ModSettingItemSpacingFactor;
        LineWidth       =  _config.ModSettingLineScale * Im.Style.GlobalScale;
        BorderWidth     =  _config.ModSettingBorderScale * Im.Style.GlobalScale;
        TextAlignment   =  _config.ModSettingLabelAlignment;
        ComboAlignment  =  _config.ModSettingComboAlignment;
        CenterSpacing   =  2 * Im.Style.ItemSpacing.X;
        Height          =  Im.Style.FrameHeight;
        Indentation     =  Height + Im.Style.ItemInnerSpacing.X;
        HalfHeight      =  MathF.Floor(Height / 2f);
        HelpIconSize    =  LunaStyle.HelpMarker.CalculateSize().X;
        CaretWidth      =  Im.Style.ItemInnerSpacing.X + Im.Style.TextHeight;
        CaretTipSpacing =  MathF.Floor(Im.Style.FramePadding.X + Im.Style.TextHeight / 2);
        DisplayDirty    =  true;
    }

    private void CreateGroupCache(IModGroup group)
    {
        var groupCache = new ModSettingGroup(group, new StringU8(group.Name), new StringU8(group.Description))
        {
            Visible     = true,
            Collapsible = true,
            HideHeader  = group.Layout.HasFlag(ModSettingsLayout.ParentHeader),
            Space       = group.Layout.HasFlag(ModSettingsLayout.Space),
            IsCombo =
                group is SingleModGroup g
             && g.Options.Count > _config.SingleGroupRadioMax, // Single options are never hidden, so this is independent of visibility
        };
        groupCache.NameWidth = groupCache.Name.CalculateSize().X + 2 * Im.Style.FramePadding.X;
        if (!groupCache.Description.IsEmpty)
            groupCache.NameWidth += Im.Style.ItemInnerSpacing.X + HelpIconSize;

        foreach (var option in group.Options)
        {
            var optionCache = CreateOptionCache(option);
            groupCache.AllOptions.Add(optionCache);
            _nodes.Add(option.Id, optionCache);
        }

        _nodes.Add(group.Id, groupCache);
        _orderedGroups.Add(groupCache);
    }

    private ModSettingOption CreateOptionCache(IModOption option)
    {
        var ret = new ModSettingOption(option, new StringU8(option.Name), new StringU8(option.Description))
        {
            Color     = option.ColorValue,
            Separator = option.Layout.HasFlag(ModSettingsLayout.Separator),
            HideLabel = option.Layout.HasFlag(ModSettingsLayout.HideOptionLabel),
            Space     = option.Layout.HasFlag(ModSettingsLayout.Space),
            Radio     = option is SingleSubMod,
        };
        ret.Width = ret.Name.CalculateSize().X;
        if (!ret.Description.IsEmpty)
            ret.Width += Im.Style.ItemInnerSpacing.X + HelpIconSize;
        return ret;
    }

    private void SetupPage(Mod mod, int pageNumber)
    {
        if (_pages.ContainsKey(pageNumber))
            return;

        var page = new ModSettingPage(pageNumber, mod.PageNames.TryGetValue(pageNumber, out var name) ? name : $"Page {pageNumber + 1}");
        _pages.Add(pageNumber, page);
    }

    private void UpdateParentage()
    {
        foreach (var group in _orderedGroups)
        {
            if (group.Group.ParentSetting is null)
            {
                _pages[group.Group.Page].Groups.Add(group);
            }
            else
            {
                var parent = _nodes[group.Group.ParentSetting.Id];
                if (parent is ModSettingOption o)
                    o.Children.Add(group);
                else
                    ((ModSettingGroup)parent).GroupChildren.Add(group);
            }
        }
    }

    #endregion

    private void OnModPathChanged(in ModPathChanged.Arguments arguments)
    {
        if (arguments.Type is ModPathChangeType.Reloaded)
            Dirty |= IManagedCache.DirtyFlags.Custom;
    }

    private void OnModSettingChanged(in ModSettingChanged.Arguments arguments)
    {
        if (!AnyConditions)
            return;

        if (arguments.Type is ModSettingChange.EnableState or ModSettingChange.Priority)
            return;

        DisplayDirty = true;
    }

    private void OnModOptionChanged(in ModOptionChanged.Arguments arguments)
    {
        if (arguments.Mod == _selection.Mod)
            Dirty |= IManagedCache.DirtyFlags.Custom;
    }

    private void OnSelectionChanged(in ModSelection.Arguments arguments)
        => Dirty |= IManagedCache.DirtyFlags.Custom;

    /// <summary> We listen to all changes in options, so the only thing we need to care about are setting changes. </summary>
    public override void SkippedRequests()
        => Dirty |= IManagedCache.DirtyFlags.Custom;

    protected override void Dispose(bool disposing)
    {
        _selection.Unsubscribe(OnSelectionChanged);
        _communicator.ModPathChanged.Unsubscribe(OnModPathChanged);
        _communicator.ModOptionChanged.Unsubscribe(OnModOptionChanged);
        _communicator.ModSettingChanged.Unsubscribe(OnModSettingChanged);
    }
}
