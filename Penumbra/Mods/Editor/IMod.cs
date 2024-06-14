using OtterGui.Classes;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.String.Classes;

namespace Penumbra.Mods.Editor;

public record struct AppliedModData(
    Dictionary<Utf8GamePath, FullPath> FileRedirections,
    MetaDictionary Manipulations)
{
    public static readonly AppliedModData Empty = new([], new MetaDictionary());
}

public interface IMod
{
    LowerString Name { get; }

    public int         Index    { get; }
    public ModPriority Priority { get; }

    public IReadOnlyList<IModGroup> Groups { get; }

    public AppliedModData GetData(ModSettings? settings = null);

    // Cache
    public int TotalManipulations { get; }
}
