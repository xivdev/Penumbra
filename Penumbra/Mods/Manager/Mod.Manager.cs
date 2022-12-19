using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Penumbra.Mods;

public sealed partial class Mod
{
    public sealed partial class Manager : IReadOnlyList< Mod >
    {
        // Set when reading Config and migrating from v4 to v5.
        public static bool MigrateModBackups = false;

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
            ModDirectoryChanged += OnModDirectoryChange;
            SetBaseDirectory( modDirectory, true );
            UpdateExportDirectory( Penumbra.Config.ExportDirectory, false );
            ModOptionChanged += OnModOptionChange;
            ModPathChanged   += OnModPathChange;
        }


        // Try to obtain a mod by its directory name (unique identifier, preferred),
        // or the first mod of the given name if no directory fits.
        public bool TryGetMod( string modDirectory, string modName, [NotNullWhen( true )] out Mod? mod )
        {
            mod = null;
            foreach( var m in _mods )
            {
                if( string.Equals(m.ModPath.Name, modDirectory, StringComparison.OrdinalIgnoreCase) )
                {
                    mod = m;
                    return true;
                }

                if( m.Name == modName )
                {
                    mod ??= m;
                }
            }

            return mod != null;
        }
    }
}