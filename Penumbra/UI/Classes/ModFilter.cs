using System;

namespace Penumbra.UI.Classes;

[Flags]
public enum ModFilter
{
    Enabled                = 1 << 0,
    Disabled               = 1 << 1,
    NoConflict             = 1 << 2,
    SolvedConflict         = 1 << 3,
    UnsolvedConflict       = 1 << 4,
    HasNoMetaManipulations = 1 << 5,
    HasMetaManipulations   = 1 << 6,
    HasNoFileSwaps         = 1 << 7,
    HasFileSwaps           = 1 << 8,
    HasConfig              = 1 << 9,
    HasNoConfig            = 1 << 10,
    HasNoFiles             = 1 << 11,
    HasFiles               = 1 << 12,
    IsNew                  = 1 << 13,
    NotNew                 = 1 << 14,
    Inherited              = 1 << 15,
    Uninherited            = 1 << 16,
    Undefined              = 1 << 17,
};

public static class ModFilterExtensions
{
    public const ModFilter UnfilteredStateMods = ( ModFilter )( ( 1 << 18 ) - 1 );

    public static string ToName( this ModFilter filter )
        => filter switch
        {
            ModFilter.Enabled                => "Enabled",
            ModFilter.Disabled               => "Disabled",
            ModFilter.NoConflict             => "No Conflicts",
            ModFilter.SolvedConflict         => "Solved Conflicts",
            ModFilter.UnsolvedConflict       => "Unsolved Conflicts",
            ModFilter.HasNoMetaManipulations => "No Meta Manipulations",
            ModFilter.HasMetaManipulations   => "Meta Manipulations",
            ModFilter.HasNoFileSwaps         => "No File Swaps",
            ModFilter.HasFileSwaps           => "File Swaps",
            ModFilter.HasNoConfig            => "No Configuration",
            ModFilter.HasConfig              => "Configuration",
            ModFilter.HasNoFiles             => "No Files",
            ModFilter.HasFiles               => "Files",
            ModFilter.IsNew                  => "Newly Imported",
            ModFilter.NotNew                 => "Not Newly Imported",
            ModFilter.Inherited              => "Inherited Configuration",
            ModFilter.Uninherited            => "Own Configuration",
            ModFilter.Undefined              => "Not Configured",
            _                                => throw new ArgumentOutOfRangeException( nameof( filter ), filter, null ),
        };
}