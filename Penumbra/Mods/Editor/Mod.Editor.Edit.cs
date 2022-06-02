using System.Collections.Generic;
using System.Linq;
using Penumbra.GameData.ByteString;
using Penumbra.Util;

namespace Penumbra.Mods;

public partial class Mod
{
    public partial class Editor
    {
        public int GroupIdx { get; private set; } = -1;
        public int OptionIdx { get; private set; }

        private IModGroup? _modGroup;
        private SubMod     _subMod;

        public ISubMod CurrentOption
            => _subMod;

        public readonly Dictionary< Utf8GamePath, FullPath > CurrentSwaps = new();

        public void SetSubMod( int groupIdx, int optionIdx )
        {
            GroupIdx  = groupIdx;
            OptionIdx = optionIdx;
            if( groupIdx >= 0 && groupIdx < _mod.Groups.Count && optionIdx >= 0 && optionIdx < _mod.Groups[ groupIdx ].Count )
            {
                _modGroup = _mod.Groups[ groupIdx ];
                _subMod   = ( SubMod )_modGroup![ optionIdx ];
            }
            else
            {
                GroupIdx  = -1;
                OptionIdx = 0;
                _modGroup = null;
                _subMod   = _mod._default;
            }

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

            Penumbra.ModManager.OptionSetFiles( _mod, GroupIdx, OptionIdx, dict );
            if( num > 0 )
                RevertFiles();
            else
                FileChanges = false;
            return num;
        }

        public void RevertFiles()
            => UpdateFiles();

        public void ApplySwaps()
        {
            Penumbra.ModManager.OptionSetFileSwaps( _mod, GroupIdx, OptionIdx, CurrentSwaps.ToDictionary( kvp => kvp.Key, kvp => kvp.Value ) );
        }

        public void RevertSwaps()
        {
            CurrentSwaps.SetTo( _subMod.FileSwaps );
        }
    }
}