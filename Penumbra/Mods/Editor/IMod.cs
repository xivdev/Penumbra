using OtterGui.Classes;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Subclasses;
using Penumbra.String.Classes;

namespace Penumbra.Mods.Editor;

public record struct AppliedModData(
    IReadOnlyCollection<KeyValuePair<Utf8GamePath, FullPath>> FileRedirections,
    IReadOnlyCollection<MetaManipulation> Manipulations)
{
    public static readonly AppliedModData Empty = new([], []);
}

public interface IMod
{
    LowerString Name { get; }

    public int         Index    { get; }
    public ModPriority Priority { get; }

    public AppliedModData GetData(ModSettings? settings = null);


    public ISubMod                  Default { get; }
    public IReadOnlyList<IModGroup> Groups  { get; }

    public IEnumerable<SubMod> AllSubMods { get; }

    // Cache
    public int TotalManipulations { get; }
}
