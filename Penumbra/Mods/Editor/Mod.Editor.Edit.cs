using System.Collections.Generic;
using System.Linq;
using Penumbra.String.Classes;
using Penumbra.Util;

namespace Penumbra.Mods;

public partial class Mod
{
    public partial class Editor
    {
        private SubMod _subMod;

        public ISubMod CurrentOption
            => _subMod;

        public readonly Dictionary< Utf8GamePath, FullPath > CurrentSwaps = new();

        public void SetSubMod( ISubMod? subMod )
        {
            _subMod = subMod as SubMod ?? _mod._default;
            UpdateFiles();
            RevertSwaps();
            RevertManipulations();
        }

        public int ApplyFiles()
        {
            var dict = new Dictionary< Utf8GamePath, FullPath >();
            var num  = 0;
            foreach( var file in _availableFiles )
            {
                foreach( var path in file.SubModUsage.Where( p => p.Item1 == CurrentOption ) )
                {
                    num += dict.TryAdd( path.Item2, file.File ) ? 0 : 1;
                }
            }

            Penumbra.ModManager.OptionSetFiles( _mod, _subMod.GroupIdx, _subMod.OptionIdx, dict );
            UpdateFiles();

            return num;
        }

        public void RevertFiles()
            => UpdateFiles();

        public void ApplySwaps()
        {
            Penumbra.ModManager.OptionSetFileSwaps( _mod, _subMod.GroupIdx, _subMod.OptionIdx, CurrentSwaps.ToDictionary( kvp => kvp.Key, kvp => kvp.Value ) );
        }

        public void RevertSwaps()
        {
            CurrentSwaps.SetTo( _subMod.FileSwaps );
        }
    }
}