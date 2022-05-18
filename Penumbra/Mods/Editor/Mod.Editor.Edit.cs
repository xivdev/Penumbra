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

        public readonly Dictionary< Utf8GamePath, FullPath > CurrentFiles         = new();
        public readonly Dictionary< Utf8GamePath, FullPath > CurrentSwaps         = new();

        public void SetSubMod( int groupIdx, int optionIdx )
        {
            GroupIdx  = groupIdx;
            OptionIdx = optionIdx;
            if( groupIdx >= 0 )
            {
                _modGroup = _mod.Groups[ groupIdx ];
                _subMod   = ( SubMod )_modGroup![ optionIdx ];
            }
            else
            {
                _modGroup = null;
                _subMod   = _mod._default;
            }

            RevertFiles();
            RevertSwaps();
            RevertManipulations();
        }

        public void ApplyFiles()
        {
            Penumbra.ModManager.OptionSetFiles( _mod, GroupIdx, OptionIdx, CurrentFiles.ToDictionary( kvp => kvp.Key, kvp => kvp.Value ) );
        }

        public void RevertFiles()
        {
            CurrentFiles.SetTo( _subMod.Files );
        }

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