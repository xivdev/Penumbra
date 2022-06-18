using System;
using System.Collections.Generic;
using OtterGui.Classes;

namespace Penumbra.Mods;

public sealed partial class Mod
{
    public class TemporaryMod : IMod
    {
        public LowerString Name { get; init; } = LowerString.Empty;
        public int Index { get; init; } = -2;
        public int Priority { get; init; } = int.MaxValue;

        public int TotalManipulations
            => Default.Manipulations.Count;

        public ISubMod Default { get; } = new SubMod();

        public IReadOnlyList< IModGroup > Groups
            => Array.Empty< IModGroup >();

        public IEnumerable< ISubMod > AllSubMods
            => new[] { Default };
    }
}