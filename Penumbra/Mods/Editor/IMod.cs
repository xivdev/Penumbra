using System.Collections.Generic;
using OtterGui.Classes;

namespace Penumbra.Mods;

public interface IMod
{
    LowerString Name { get; }

    public int Index { get; }
    public int Priority { get; }

    public ISubMod Default { get; }
    public IReadOnlyList< IModGroup > Groups { get; }

    public IEnumerable< ISubMod > AllSubMods { get; }
}