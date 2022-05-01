using System.Collections.Generic;
using Penumbra.GameData.ByteString;
using Penumbra.Meta.Manipulations;
using Penumbra.Util;

namespace Penumbra.Mods;

public partial class Mod
{
    public partial class Editor
    {
        private int _groupIdx  = -1;
        private int _optionIdx = 0;

        private IModGroup? _modGroup;
        private SubMod     _subMod;

        public readonly Dictionary< Utf8GamePath, FullPath > CurrentFiles         = new();
        public readonly Dictionary< Utf8GamePath, FullPath > CurrentSwaps         = new();
        public readonly HashSet< MetaManipulation >          CurrentManipulations = new();

        public void SetSubMod( int groupIdx, int optionIdx )
        {
            _groupIdx  = groupIdx;
            _optionIdx = optionIdx;
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

            CurrentFiles.SetTo( _subMod.Files );
            CurrentSwaps.SetTo( _subMod.FileSwaps );
            CurrentManipulations.Clear();
            CurrentManipulations.UnionWith( _subMod.Manipulations );
        }
    }
}