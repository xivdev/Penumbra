using System;
using System.Collections;
using System.Collections.Generic;

namespace Penumbra.Mods;

public sealed partial class Mod2
{
    public sealed partial class Manager : IEnumerable< Mod2 >
    {
        private readonly List< Mod2 > _mods = new();

        public Mod2 this[ Index idx ]
            => _mods[ idx ];

        public IReadOnlyList< Mod2 > Mods
            => _mods;

        public int Count
            => _mods.Count;

        public IEnumerator< Mod2 > GetEnumerator()
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