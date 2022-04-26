using System;
using System.Collections;
using System.Collections.Generic;

namespace Penumbra.Mods;

public sealed partial class Mod
{
    public sealed partial class Manager : IEnumerable< Mod >
    {
        private readonly List< Mod > _mods = new();

        public Mod this[ Index idx ]
            => _mods[ idx ];

        public IReadOnlyList< Mod > Mods
            => _mods;

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
        }
    }
}