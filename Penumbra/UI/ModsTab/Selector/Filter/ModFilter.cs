using ImSharp;
using Luna;
using Penumbra.Collections.Manager;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Settings;
using Penumbra.UI.Classes;
using static ImSharp.Im;

namespace Penumbra.UI.ModsTab.Selector;

public sealed class ModFilter(ModManager modManager, ActiveCollections collections)
    : TokenizedFilter<ModFilterTokenType, ModFileSystemCache.ModData, ModFilterToken>,
        IFileSystemFilter<ModFileSystemCache.ModData>
{
    private ModTypeFilter _stateFilter = ModTypeFilterExtensions.UnfilteredStateMods;

    public ModTypeFilter StateFilter
        => _stateFilter;

    protected override void DrawTooltip()
    {
        if (!Item.Hovered())
            return;

        using var tt             = Tooltip.Begin();
        var       highlightColor = ColorId.NewMod.Value().ToVector();
        Im.Text("Filter mods for those where their full paths or names contain the given strings, split by spaces."u8);
        ImEx.TextMultiColored("Enter "u8).Then("c:[string]"u8, highlightColor).Then(" to filter for mods changing specific items."u8).End();
        ImEx.TextMultiColored("Enter "u8).Then("t:[string]"u8, highlightColor).Then(" to filter for mods set to specific tags."u8).End();
        ImEx.TextMultiColored("Enter "u8).Then("n:[string]"u8, highlightColor)
            .Then(" to filter for mods names without considering the paths."u8).End();
        ImEx.TextMultiColored("Enter "u8).Then("a:[string]"u8, highlightColor).Then(" to filter for mods by specific authors."u8).End();
        ImEx.TextMultiColored("Enter "u8).Then("s:[string]"u8, highlightColor).Then(
                $" to filter for mods by the categories of the items they change (use 1-{ChangedItemFlagExtensions.NumCategories + 1} or a partial category name).")
            .End();
        Line.New();
        ImEx.TextMultiColored("Use "u8).Then("None"u8, highlightColor).Then(" as a placeholder value that only matches empty lists or names."u8)
            .End();
        Im.Text("Regularly, a mod has to match all supplied criteria separately."u8);
        ImEx.TextMultiColored("Put a "u8).Then("'-'"u8, highlightColor)
            .Then(" in front of a search token to search only for mods not matching the criterion."u8).End();
        ImEx.TextMultiColored("Put a "u8).Then("'?'"u8, highlightColor)
            .Then(" in front of a search token to search for mods matching at least one of the '?'-criteria."u8).End();
        ImEx.TextMultiColored("Wrap spaces in "u8).Then("\"[string with space]\""u8, highlightColor)
            .Then(" to match this exact combination of words."u8).End();
        Line.New();
        Im.Text("Example: 't:Tag1 t:\"Tag 2\" -t:Tag3 -a:None s:Body -c:Hempen ?c:Camise ?n:Top' will match any mod that"u8);
        BulletText("contains the tags 'tag1' and 'tag2',"u8);
        BulletText("does not contain the tag 'tag3',"u8);
        BulletText("has any author set (negating None means Any),"u8);
        BulletText("changes an item of the 'Body' category,"u8);
        BulletText("and either contains a changed item with 'camise' in it's name, or has 'top' in the mod's name."u8);
    }

    public override bool DrawFilter(ReadOnlySpan<byte> label, Vector2 availableRegion)
    {
        var ret = base.DrawFilter(label, availableRegion with { X = availableRegion.X - Style.FrameHeight });
        Line.NoSpacing();
        ret |= DrawFilterCombo();
        return ret;
    }

    private bool DrawFilterCombo()
    {
        var       everything = _stateFilter is not ModTypeFilterExtensions.UnfilteredStateMods;
        using var color      = ImGuiColor.Button.Push(Colors.FilterActive, everything);
        using var combo = Combo.Begin("##combo"u8, StringU8.Empty,
            ComboFlags.NoPreview | ComboFlags.HeightLargest | ComboFlags.PopupAlignLeft);

        if (Item.RightClicked())
        {
            // Ensure that a right-click clears the text filter if it is currently being edited.
            Id.ClearActive();
            Clear();
        }

        Tooltip.OnHover("Filter mods for their activation status.\nRight-Click to clear all filters, including the text-filter."u8);

        var changes = false;
        if (combo)
        {
            using var style = ImStyleDouble.ItemSpacing.PushY(3 * Style.GlobalScale);
            changes |= Checkbox("Everything"u8, ref _stateFilter, ModTypeFilterExtensions.UnfilteredStateMods);
            Dummy(new Vector2(0, 5 * Style.GlobalScale));
            foreach (var (onFlag, offFlag, name) in ModTypeFilterExtensions.TriStatePairs)
                changes |= ImEx.TriStateCheckbox(name, ref _stateFilter, onFlag, offFlag);

            foreach (var group in ModTypeFilterExtensions.Groups)
            {
                Separator();
                foreach (var (flag, name) in group)
                    changes |= Checkbox(name, ref _stateFilter, flag);
            }
        }

        if (changes)
            InvokeEvent();

        return changes;
    }

    public override void Clear()
    {
        var changes = _stateFilter is not ModTypeFilterExtensions.UnfilteredStateMods;
        _stateFilter = ModTypeFilterExtensions.UnfilteredStateMods;
        if (!Set(string.Empty) && changes)
            InvokeEvent();
    }

    protected override bool Matches(in ModFilterToken token, in ModFileSystemCache.ModData cacheItem)
        => token.Type switch
        {
            ModFilterTokenType.Default => cacheItem.Node.FullPath.Contains(token.Needle, StringComparison.OrdinalIgnoreCase)
             || cacheItem.Node.Value.Name.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
            ModFilterTokenType.ChangedItem => cacheItem.Node.Value.LowerChangedItemsString.Contains(token.Needle, StringComparison.Ordinal),
            ModFilterTokenType.Tag         => cacheItem.Node.Value.AllTagsLower.Contains(token.Needle, StringComparison.Ordinal),
            ModFilterTokenType.Name        => cacheItem.Node.Value.Name.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
            ModFilterTokenType.Author      => cacheItem.Node.Value.Author.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
            ModFilterTokenType.Category    => CheckCategory(token.IconFlagFilter, cacheItem),
            _                              => true,
        };

    private static bool CheckCategory(ChangedItemIconFlag flag, ModFileSystemCache.ModData cacheItem)
        => cacheItem.Node.Value.ChangedItems.Any(p => (p.Value.Icon.ToFlag() & flag) is not 0);

    protected override bool MatchesNone(ModFilterTokenType type, bool negated, in ModFileSystemCache.ModData cacheItem)
        => type switch
        {
            ModFilterTokenType.Author when negated      => cacheItem.Node.Value.Author.Length > 0,
            ModFilterTokenType.Author                   => cacheItem.Node.Value.Author.Length is 0,
            ModFilterTokenType.ChangedItem when negated => cacheItem.Node.Value.LowerChangedItemsString.Length > 0,
            ModFilterTokenType.ChangedItem              => cacheItem.Node.Value.LowerChangedItemsString.Length is 0,
            ModFilterTokenType.Tag when negated         => cacheItem.Node.Value.AllTagsLower.Length > 0,
            ModFilterTokenType.Tag                      => cacheItem.Node.Value.AllTagsLower.Length is 0,
            ModFilterTokenType.Category when negated    => cacheItem.Node.Value.ChangedItems.Count > 0,
            ModFilterTokenType.Category                 => cacheItem.Node.Value.ChangedItems.Count is 0,
            _                                           => true,
        };

    public override bool WouldBeVisible(in ModFileSystemCache.ModData cacheItem, int globalIndex)
    {
        if (!base.WouldBeVisible(in cacheItem, globalIndex))
            return false;

        if (_stateFilter is ModTypeFilterExtensions.UnfilteredStateMods)
            return true;

        return CheckStateFilters(cacheItem.Node.Value);
    }

    private bool CheckStateFilters(Mod mod)
    {
        var (settings, collection) = collections.Current.GetActualSettings(mod.Index);
        var isNew = modManager.IsNew(mod);
        // Handle mod details.
        if (CheckFlags(mod.TotalFileCount,     ModTypeFilter.HasNoFiles,             ModTypeFilter.HasFiles)
         || CheckFlags(mod.TotalSwapCount,     ModTypeFilter.HasNoFileSwaps,         ModTypeFilter.HasFileSwaps)
         || CheckFlags(mod.TotalManipulations, ModTypeFilter.HasNoMetaManipulations, ModTypeFilter.HasMetaManipulations)
         || CheckFlags(mod.HasOptions ? 1 : 0, ModTypeFilter.HasNoConfig,            ModTypeFilter.HasConfig)
         || CheckFlags(isNew ? 1 : 0,          ModTypeFilter.NotNew,                 ModTypeFilter.IsNew))
            return false;

        // Handle Favoritism
        if (!_stateFilter.HasFlag(ModTypeFilter.Favorite) && mod.Favorite
         || !_stateFilter.HasFlag(ModTypeFilter.NotFavorite) && !mod.Favorite)
            return false;

        // Handle Temporary
        if (!_stateFilter.HasFlag(ModTypeFilter.Temporary) || !_stateFilter.HasFlag(ModTypeFilter.NotTemporary))
        {
            if (settings is null && _stateFilter.HasFlag(ModTypeFilter.Temporary))
                return false;

            if (settings is not null && settings.IsTemporary() != _stateFilter.HasFlag(ModTypeFilter.Temporary))
                return false;
        }

        // Handle Inheritance
        if (collection == collections.Current)
        {
            if (!_stateFilter.HasFlag(ModTypeFilter.Uninherited))
                return false;
        }
        else
        {
            if (!_stateFilter.HasFlag(ModTypeFilter.Inherited))
                return false;
        }

        // Handle settings.
        if (settings is null)
        {
            if (!_stateFilter.HasFlag(ModTypeFilter.Undefined)
             || !_stateFilter.HasFlag(ModTypeFilter.Disabled)
             || !_stateFilter.HasFlag(ModTypeFilter.NoConflict))
                return false;
        }
        else if (!settings.Enabled)
        {
            if (!_stateFilter.HasFlag(ModTypeFilter.Disabled)
             || !_stateFilter.HasFlag(ModTypeFilter.NoConflict))
                return false;
        }
        else
        {
            if (!_stateFilter.HasFlag(ModTypeFilter.Enabled))
                return false;

            // Conflicts can only be relevant if the mod is enabled.
            var conflicts = collections.Current.Conflicts(mod);
            if (conflicts.Count > 0)
            {
                if (conflicts.Any(c => !c.Solved))
                {
                    if (!_stateFilter.HasFlag(ModTypeFilter.UnsolvedConflict))
                        return false;
                }
                else
                {
                    if (!_stateFilter.HasFlag(ModTypeFilter.SolvedConflict))
                        return false;
                }
            }
            else if (!_stateFilter.HasFlag(ModTypeFilter.NoConflict))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Check the state filter for a specific pair of has/has-not flags.
    /// Uses count == 0 to check for has-not and count != 0 for has.
    /// Returns true if it should be filtered and false if not. 
    /// </summary>
    private bool CheckFlags(int count, ModTypeFilter hasNoFlag, ModTypeFilter hasFlag)
        => count switch
        {
            0 when _stateFilter.HasFlag(hasNoFlag) => false,
            0                                      => true,
            _ when _stateFilter.HasFlag(hasFlag)   => false,
            _                                      => true,
        };

    public bool WouldBeVisible(in FileSystemFolderCache folder)
    {
        if (_stateFilter is not ModTypeFilterExtensions.UnfilteredStateMods)
            return false;

        switch (State)
        {
            case FilterState.NoFilters: return true;
            case FilterState.NoMatches: return false;
        }

        foreach (var token in Forced)
        {
            if (token.Type switch
                {
                    ModFilterTokenType.Name    => folder.Name.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
                    ModFilterTokenType.Default => folder.FullPath.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
                    _                          => true,
                })
                return false;
        }

        foreach (var token in Negated)
        {
            if (token.Type switch
                {
                    ModFilterTokenType.Name    => folder.Name.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
                    ModFilterTokenType.Default => folder.FullPath.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
                    _                          => false,
                })
                return false;
        }

        foreach (var token in General)
        {
            if (token.Type switch
                {
                    ModFilterTokenType.Name    => folder.Name.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
                    ModFilterTokenType.Default => folder.FullPath.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
                    _                          => false,
                })
                return true;
        }

        return General.Count is 0;
    }
}
