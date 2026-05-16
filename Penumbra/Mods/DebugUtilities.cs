using Luna;
using Penumbra.Mods.Groups;
using Penumbra.Mods.SubMods;
using Penumbra.Util;

namespace Penumbra.Mods;

public static class DebugUtilities
{
    /// <summary> Compare two mods for exact equality in all their parsed properties. </summary>
    public static bool CompareMod(Mod lhs, Mod rhs, bool checkLocal)
    {
        // Meta
        // Do not check StableIdentifier due to NewGuid().
        if (lhs.Name != rhs.Name)
            return false;
        if (lhs.Author != rhs.Author)
            return false;
        if (lhs.Description != rhs.Description)
            return false;
        if (lhs.Version != rhs.Version)
            return false;
        if (lhs.Website != rhs.Website)
            return false;
        if (lhs.Image != rhs.Image)
            return false;
        if (!lhs.ModTags.SequenceEqual(rhs.ModTags))
            return false;
        if (!lhs.DefaultPreferredItems.SetEquals(rhs.DefaultPreferredItems))
            return false;
        if (lhs.RequiredFeatures != rhs.RequiredFeatures)
            return false;

        // Local
        if (checkLocal)
        {
            if (lhs.Path.Folder != rhs.Path.Folder)
                return false; // Skip node because duplication.
            if (lhs.ImportDate != rhs.ImportDate)
                return false;
            if (lhs.LastConfigEdit != rhs.LastConfigEdit)
                return false;
            if (lhs.IgnoreLastConfig != rhs.IgnoreLastConfig)
                return false;
            if (!lhs.LocalTags.SequenceEqual(rhs.LocalTags))
                return false;
            if (lhs.Note != rhs.Note)
                return false;
            if (!lhs.PreferredChangedItems.SetEquals(rhs.PreferredChangedItems))
                return false;
            if (lhs.Favorite != rhs.Favorite)
                return false;
        }

        // Data
        if (!CheckContainer(lhs.Default, rhs.Default))
            return false;

        if (lhs.Groups.Count != rhs.Groups.Count)
            return false;

        foreach (var (lGroup, rGroup) in lhs.Groups.Zip(rhs.Groups))
        {
            if (!CheckGroup(lGroup, rGroup))
                return false;
        }

        return true;
    }

    private static bool CheckSubObject(IModObject lhs, IModObject rhs)
    {
        // Skipped ID because of NewGuid().
        if (lhs.Name != rhs.Name)
            return false;
        if (lhs.Description != rhs.Description)
            return false;
        if (lhs.Layout != rhs.Layout)
            return false;
        if (!EqualityComparer<ICondition<ModSettingContext>>.Default.Equals(lhs.Condition, rhs.Condition))
            return false;

        return true;
    }

    private static bool CheckGroup(IModGroup lGroup, IModGroup rGroup)
    {
        if (lGroup.Type != rGroup.Type)
            return false;
        if (!CheckSubObject(lGroup, rGroup))
            return false;

        if (lGroup.DefaultSettings != rGroup.DefaultSettings)
            return false;
        if (lGroup.Image != rGroup.Image)
            return false;
        if (lGroup.Page != rGroup.Page)
            return false;
        if (lGroup.Priority != rGroup.Priority)
            return false;
        if (lGroup.DefaultSettings != rGroup.DefaultSettings)
            return false;
        if (lGroup.ParentSetting != rGroup.ParentSetting)
            return false;

        if (lGroup is ImcModGroup l)
        {
            if (rGroup is not ImcModGroup r)
                return false;

            if (l.AllVariants != r.AllVariants)
                return false;
            if (l.CanBeDisabled != r.CanBeDisabled)
                return false;
            if (l.OnlyAttributes != r.OnlyAttributes)
                return false;
            if (!l.Identifier.Equals(r.Identifier))
                return false;
            if (!l.DefaultEntry.Equals(r.DefaultEntry))
                return false;
        }

        if (lGroup.Options.Count != rGroup.Options.Count)
            return false;
        if (lGroup.DataContainers.Count != rGroup.DataContainers.Count)
            return false;

        foreach (var (lOption, rOption) in lGroup.Options.Zip(rGroup.Options))
        {
            if (!CheckOption(lOption, rOption))
                return false;
        }

        foreach (var (lContainer, rContainer) in lGroup.DataContainers.Zip(rGroup.DataContainers))
        {
            if (!CheckContainer(lContainer, rContainer))
                return false;
        }

        return true;
    }

    private static bool CheckOption(IModOption lhs, IModOption rhs)
    {
        if (!CheckSubObject(lhs, rhs))
            return false;

        switch (lhs, rhs)
        {
            case (MultiSubMod l, MultiSubMod r): return l.Priority == r.Priority;
            case (ImcSubMod l, ImcSubMod r):
                if (l.IsDisableSubMod != r.IsDisableSubMod)
                    return false;

                return l.AttributeMask == r.AttributeMask;
        }

        return true;
    }

    private static bool CheckContainer(IModDataContainer lhs, IModDataContainer rhs)
    {
        if (!lhs.Files.SetEquals(rhs.Files))
            return false;
        if (!lhs.FileSwaps.SetEquals(rhs.FileSwaps))
            return false;
        if (!lhs.Manipulations.Equals(rhs.Manipulations))
            return false;

        if (lhs is CombinedDataContainer l)
        {
            if (rhs is not CombinedDataContainer r)
                return false;

            return l.Name == r.Name;
        }

        return true;
    }
}
