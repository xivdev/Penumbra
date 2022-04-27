using System;
using System.Collections;
using System.Collections.Generic;

namespace Penumbra.Mods;

public sealed partial class Mod
{
    public sealed partial class Manager : IReadOnlyList< Mod >
    {
        // An easily accessible set of new mods.
        // Mods are added when they are created or imported.
        // Mods are removed when they are deleted or when they are toggled in any collection.
        // Also gets cleared on mod rediscovery.
        public readonly HashSet< Mod > NewMods = new();

        private readonly List< Mod > _mods = new();

        public Mod this[ int idx ]
            => _mods[ idx ];

        public Mod this[ Index idx ]
            => _mods[ idx ];

        public int Count
            => _mods.Count;

        public IEnumerator< Mod > GetEnumerator()
            => _mods.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        public Manager( string modDirectory )
        {
            SetBaseDirectory( modDirectory, true );
            ModOptionChanged += OnModOptionChange;
            ModPathChanged   += OnModPathChange;
        }
    }
}