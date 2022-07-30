using System;
using System.Collections.Generic;
using OtterGui.Classes;
using Penumbra.GameData.ByteString;
using Penumbra.Meta.Manipulations;

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

        public ISubMod Default
            => _default;

        public IReadOnlyList< IModGroup > Groups
            => Array.Empty< IModGroup >();

        public IEnumerable< ISubMod > AllSubMods
            => new[] { Default };

        private readonly SubMod _default;

        public TemporaryMod()
            => _default = new SubMod( this );

        public void SetFile( Utf8GamePath gamePath, FullPath fullPath )
            => _default.FileData[ gamePath ] = fullPath;

        public bool SetManipulation( MetaManipulation manip )
            => _default.ManipulationData.Remove( manip ) | _default.ManipulationData.Add( manip );

        public void SetAll( Dictionary< Utf8GamePath, FullPath > dict, HashSet< MetaManipulation > manips )
        {
            _default.FileData         = dict;
            _default.ManipulationData = manips;
        }
    }
}