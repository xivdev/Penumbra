using Penumbra.Api.Enums;
using Penumbra.Mods.Settings;

namespace Penumbra.Mods.Groups;

public static class ModGroup
{
    /// <summary> Create a new mod group based on the given type. </summary>
    public static IModGroup Create(Mod mod, GroupType type, string name)
    {
        var maxPriority = mod.Groups.Count == 0 ? ModPriority.Default : mod.Groups.Max(o => o.Priority) + 1;
        return type switch
        {
            GroupType.Single => new SingleModGroup(mod)
            {
                Name     = name,
                Priority = maxPriority,
            },
            GroupType.Multi => new MultiModGroup(mod)
            {
                Name     = name,
                Priority = maxPriority,
            },
            GroupType.Imc => new ImcModGroup(mod)
            {
                Name     = name,
                Priority = maxPriority,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }


    public static (int Redirections, int Swaps, int Manips) GetCountsBase(IModGroup group)
    {
        var redirectionCount = 0;
        var swapCount        = 0;
        var manipCount       = 0;
        foreach (var option in group.DataContainers)
        {
            redirectionCount += option.Files.Count;
            swapCount        += option.FileSwaps.Count;
            manipCount       += option.Manipulations.Count;
        }

        return (redirectionCount, swapCount, manipCount);
    }

    public static int GetIndex(IModGroup group)
    {
        var groupIndex = group.Mod.Groups.IndexOf(group);
        if (groupIndex < 0)
            throw new Exception($"Mod {group.Mod.Name} from Group {group.Name} does not contain this group.");

        return groupIndex;
    }
}
