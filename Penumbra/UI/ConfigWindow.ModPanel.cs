using System;
using OtterGui.Widgets;
using Penumbra.Mods;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public partial class ConfigWindow
{

    // The basic setup for the mod panel.
    // Details are in other files.
    private partial class ModPanel : IDisposable
    {
        private readonly ConfigWindow _window;

        private          bool               _valid;
        private          ModFileSystem.Leaf _leaf      = null!;
        private          Mod                _mod       = null!;
        private readonly TagButtons         _localTags = new();

        public ModPanel( ConfigWindow window )
            => _window = window;

        public void Dispose()
        {
            _nameFont.Dispose();
        }

        public void Draw( ModFileSystemSelector selector )
        {
            Init( selector );
            if( !_valid )
            {
                return;
            }

            DrawModHeader();
            DrawTabBar();
        }

        private void Init( ModFileSystemSelector selector )
        {
            _valid = selector.Selected != null;
            if( !_valid )
            {
                return;
            }

            _leaf = selector.SelectedLeaf!;
            _mod  = selector.Selected!;
            UpdateSettingsData( selector );
            UpdateModData();
        }

        public void OnSelectionChange( Mod? old, Mod? mod, in ModFileSystemSelector.ModState _ )
        {
            if( old == mod )
            {
                return;
            }

            if( mod == null )
            {
                _window.ModEditPopup.IsOpen = false;
            }
            else if( _window.ModEditPopup.IsOpen )
            {
                _window.ModEditPopup.ChangeMod( mod );
            }

            _currentPriority = null;
            MoveDirectory.Reset();
            OptionTable.Reset();
            Input.Reset();
            AddOptionGroup.Reset();
        }
    }
}