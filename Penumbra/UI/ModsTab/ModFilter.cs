namespace Penumbra.UI.ModsTab;

[Flags]
public enum ModFilter
{
    Enabled                = 1 << 0,
    Disabled               = 1 << 1,
    Favorite               = 1 << 2,
    NotFavorite            = 1 << 3,
    NoConflict             = 1 << 4,
    SolvedConflict         = 1 << 5,
    UnsolvedConflict       = 1 << 6,
    HasNoMetaManipulations = 1 << 7,
    HasMetaManipulations   = 1 << 8,
    HasNoFileSwaps         = 1 << 9,
    HasFileSwaps           = 1 << 10,
    HasConfig              = 1 << 11,
    HasNoConfig            = 1 << 12,
    HasNoFiles             = 1 << 13,
    HasFiles               = 1 << 14,
    IsNew                  = 1 << 15,
    NotNew                 = 1 << 16,
    Inherited              = 1 << 17,
    Uninherited            = 1 << 18,
    Undefined              = 1 << 19,
};

public static class ModFilterExtensions
{
    public const ModFilter UnfilteredStateMods = (ModFilter)((1 << 20) - 1);

    public static IReadOnlyList<(ModFilter On, ModFilter Off, string Name)> TriStatePairs =
    [
        (ModFilter.Enabled, ModFilter.Disabled, "Enabled"),
        (ModFilter.IsNew, ModFilter.NotNew, "Newly Imported"),
        (ModFilter.Favorite, ModFilter.NotFavorite, "Favorite"),
        (ModFilter.HasConfig, ModFilter.HasNoConfig, "Has Options"),
        (ModFilter.HasFiles, ModFilter.HasNoFiles, "Has Redirections"),
        (ModFilter.HasMetaManipulations, ModFilter.HasNoMetaManipulations, "Has Meta Manipulations"),
        (ModFilter.HasFileSwaps, ModFilter.HasNoFileSwaps, "Has File Swaps"),
    ];

    public static IReadOnlyList<IReadOnlyList<(ModFilter Filter, string Name)>> Groups =
    [
        [
            (ModFilter.NoConflict, "Has No Conflicts"),
            (ModFilter.SolvedConflict, "Has Solved Conflicts"),
            (ModFilter.UnsolvedConflict, "Has Unsolved Conflicts"),
        ],
        [
            (ModFilter.Undefined, "Not Configured"),
            (ModFilter.Inherited, "Inherited Configuration"),
            (ModFilter.Uninherited, "Own Configuration"),
        ],
    ];
}
