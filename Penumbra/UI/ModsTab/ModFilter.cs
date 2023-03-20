using System;

namespace Penumbra.UI.ModsTab;

[Flags]
public enum ModFilter
{
    Enabled = 1 << 0,
    Disabled = 1 << 1,
    Favorite = 1 << 2,
    NotFavorite = 1 << 3,
    NoConflict = 1 << 4,
    SolvedConflict = 1 << 5,
    UnsolvedConflict = 1 << 6,
    HasNoMetaManipulations = 1 << 7,
    HasMetaManipulations = 1 << 8,
    HasNoFileSwaps = 1 << 9,
    HasFileSwaps = 1 << 10,
    HasConfig = 1 << 11,
    HasNoConfig = 1 << 12,
    HasNoFiles = 1 << 13,
    HasFiles = 1 << 14,
    IsNew = 1 << 15,
    NotNew = 1 << 16,
    Inherited = 1 << 17,
    Uninherited = 1 << 18,
    Undefined = 1 << 19,
};

public static class ModFilterExtensions
{
    public const ModFilter UnfilteredStateMods = (ModFilter)((1 << 20) - 1);

    public static string ToName(this ModFilter filter)
        => filter switch
        {
            ModFilter.Enabled => "Enabled",
            ModFilter.Disabled => "Disabled",
            ModFilter.Favorite => "Favorite",
            ModFilter.NotFavorite => "No Favorite",
            ModFilter.NoConflict => "No Conflicts",
            ModFilter.SolvedConflict => "Solved Conflicts",
            ModFilter.UnsolvedConflict => "Unsolved Conflicts",
            ModFilter.HasNoMetaManipulations => "No Meta Manipulations",
            ModFilter.HasMetaManipulations => "Meta Manipulations",
            ModFilter.HasNoFileSwaps => "No File Swaps",
            ModFilter.HasFileSwaps => "File Swaps",
            ModFilter.HasNoConfig => "No Configuration",
            ModFilter.HasConfig => "Configuration",
            ModFilter.HasNoFiles => "No Files",
            ModFilter.HasFiles => "Files",
            ModFilter.IsNew => "Newly Imported",
            ModFilter.NotNew => "Not Newly Imported",
            ModFilter.Inherited => "Inherited Configuration",
            ModFilter.Uninherited => "Own Configuration",
            ModFilter.Undefined => "Not Configured",
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null),
        };
}