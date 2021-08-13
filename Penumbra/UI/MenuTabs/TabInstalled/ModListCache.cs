using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Penumbra.Mods;

namespace Penumbra.UI
{
    public class ModListCache : IDisposable
    {
        public const uint DisabledModColor        = 0xFF666666u;
        public const uint ConflictingModColor     = 0xFFAAAAFFu;
        public const uint HandledConflictModColor = 0xFF88DDDDu;

        private readonly ModManager _manager;

        private readonly List< Mod.Mod >                                       _modsInOrder    = new();
        private readonly List< (bool visible, uint color) >                    _visibleMods    = new();
        private readonly Dictionary< ModFolder, (bool visible, bool enabled) > _visibleFolders = new();

        private string    _modFilter            = "";
        private string    _modFilterChanges     = "";
        private string    _modFilterAuthor      = "";
        private ModFilter _stateFilter          = ModFilterExtensions.UnfilteredStateMods;
        private bool      _listResetNecessary   = false;
        private bool      _filterResetNecessary = false;


        public ModFilter StateFilter
        {
            get => _stateFilter;
            set
            {
                var diff = _stateFilter != value;
                _stateFilter = value;
                if( diff )
                {
                    TriggerFilterReset();
                }
            }
        }

        public ModListCache( ModManager manager )
        {
            _manager = manager;
            ResetModList();
            ModFileSystem.ModFileSystemChanged += TriggerListReset;
        }

        public void Dispose()
        {
            ModFileSystem.ModFileSystemChanged -= TriggerListReset;
        }

        public int Count
            => _modsInOrder.Count;


        public bool Update()
        {
            if( _listResetNecessary )
            {
                ResetModList();
                return true;
            }

            if( _filterResetNecessary )
            {
                ResetFilters();
                return true;
            }

            return false;
        }

        public void TriggerListReset()
            => _listResetNecessary = true;

        public void TriggerFilterReset()
            => _filterResetNecessary = true;

        public void RemoveMod( Mod.Mod mod )
        {
            var idx = _modsInOrder.IndexOf( mod );
            if( idx >= 0 )
            {
                _modsInOrder.RemoveAt( idx );
                _visibleMods.RemoveAt( idx );
                UpdateFolders();
            }
        }

        private void SetFolderAndParentsVisible( ModFolder? folder )
        {
            while( folder != null && ( !_visibleFolders.TryGetValue( folder, out var state ) || !state.visible ) )
            {
                _visibleFolders[ folder ] = ( true, true );
                folder                    = folder.Parent;
            }
        }

        private void UpdateFolders()
        {
            _visibleFolders.Clear();

            for( var i = 0; i < _modsInOrder.Count; ++i )
            {
                if( _visibleMods[ i ].visible )
                {
                    SetFolderAndParentsVisible( _modsInOrder[ i ].Data.SortOrder.ParentFolder );
                }
            }
        }

        public void SetTextFilter( string filter )
        {
            var lower = filter.ToLowerInvariant();
            if( lower.StartsWith( "c:" ) )
            {
                _modFilterChanges = lower.Substring( 2 );
                _modFilter        = string.Empty;
                _modFilterAuthor  = string.Empty;
            }
            else if( lower.StartsWith( "a:" ) )
            {
                _modFilterAuthor  = lower.Substring( 2 );
                _modFilter        = string.Empty;
                _modFilterChanges = string.Empty;
            }
            else
            {
                _modFilter        = lower;
                _modFilterAuthor  = string.Empty;
                _modFilterChanges = string.Empty;
            }

            ResetFilters();
        }

        private void ResetModList()
        {
            _modsInOrder.Clear();
            _visibleMods.Clear();
            _visibleFolders.Clear();

            PluginLog.Debug( "Resetting mod selector list..." );
            if( !_modsInOrder.Any() )
            {
                foreach( var modData in _manager.StructuredMods.AllMods( _manager.Config.SortFoldersFirst ) )
                {
                    var mod = _manager.Collections.CurrentCollection.GetMod( modData );
                    _modsInOrder.Add( mod );
                    _visibleMods.Add( CheckFilters( mod ) );
                }
            }

            _listResetNecessary   = false;
            _filterResetNecessary = false;
        }

        private void ResetFilters()
        {
            _visibleMods.Clear();
            _visibleFolders.Clear();
            PluginLog.Debug( "Resetting mod selector filters..." );
            foreach( var mod in _modsInOrder )
            {
                _visibleMods.Add( CheckFilters( mod ) );
            }
            _filterResetNecessary = false;
        }

        public (Mod.Mod? mod, int idx) GetModByName( string name )
        {
            for( var i = 0; i < Count; ++i )
            {
                if( _modsInOrder[ i ].Data.Meta.Name == name )
                {
                    return ( _modsInOrder[ i ], i );
                }
            }

            return ( null, 0 );
        }

        public (Mod.Mod? mod, int idx) GetModByBasePath( string basePath )
        {
            for( var i = 0; i < Count; ++i )
            {
                if( _modsInOrder[ i ].Data.BasePath.Name == basePath )
                {
                    return ( _modsInOrder[ i ], i );
                }
            }

            return ( null, 0 );
        }

        public (bool visible, bool enabled) GetFolder( ModFolder folder )
            => _visibleFolders.TryGetValue( folder, out var ret ) ? ret : ( false, false );

        public (Mod.Mod?, bool visible, uint color) GetMod( int idx )
            => idx >= 0 && idx < _modsInOrder.Count
                ? ( _modsInOrder[ idx ], _visibleMods[ idx ].visible, _visibleMods[ idx ].color )
                : ( null, false, 0 );

        private bool CheckFlags( int count, ModFilter hasNoFlag, ModFilter hasFlag )
        {
            if( count == 0 )
            {
                if( StateFilter.HasFlag( hasNoFlag ) )
                {
                    return false;
                }
            }
            else if( StateFilter.HasFlag( hasFlag ) )
            {
                return false;
            }

            return true;
        }

        private (bool, uint) CheckFilters( Mod.Mod mod )
        {
            var ret = ( false, 0u );
            if( _modFilter.Any() && !mod.Data.Meta.LowerName.Contains( _modFilter ) )
            {
                return ret;
            }

            if( _modFilterAuthor.Any() && !mod.Data.Meta.LowerAuthor.Contains( _modFilterAuthor ) )
            {
                return ret;
            }

            if( _modFilterChanges.Any() && !mod.Data.LowerChangedItemsString.Contains( _modFilterChanges ) )
            {
                return ret;
            }

            if( CheckFlags( mod.Data.Resources.ModFiles.Count, ModFilter.HasNoFiles, ModFilter.HasFiles ) )
            {
                return ret;
            }

            if( CheckFlags( mod.Data.Meta.FileSwaps.Count, ModFilter.HasNoFileSwaps, ModFilter.HasFileSwaps ) )
            {
                return ret;
            }

            if( CheckFlags( mod.Data.Resources.MetaManipulations.Count, ModFilter.HasNoMetaManipulations,
                ModFilter.HasMetaManipulations ) )
            {
                return ret;
            }

            if( CheckFlags( mod.Data.Meta.HasGroupsWithConfig ? 1 : 0, ModFilter.HasNoConfig, ModFilter.HasConfig ) )
            {
                return ret;
            }

            if( !mod.Settings.Enabled )
            {
                if( !StateFilter.HasFlag( ModFilter.Disabled ) || !StateFilter.HasFlag( ModFilter.NoConflict ) )
                {
                    return ret;
                }

                ret.Item2 = ret.Item2 == 0 ? DisabledModColor : ret.Item2;
            }

            if( mod.Settings.Enabled && !StateFilter.HasFlag( ModFilter.Enabled ) )
            {
                return ret;
            }

            if( mod.Cache.Conflicts.Any() )
            {
                if( mod.Cache.Conflicts.Keys.Any( m => m.Settings.Priority == mod.Settings.Priority ) )
                {
                    if( !StateFilter.HasFlag( ModFilter.UnsolvedConflict ) )
                    {
                        return ret;
                    }

                    ret.Item2 = ret.Item2 == 0 ? ConflictingModColor : ret.Item2;
                }
                else
                {
                    if( !StateFilter.HasFlag( ModFilter.SolvedConflict ) )
                    {
                        return ret;
                    }

                    ret.Item2 = ret.Item2 == 0 ? HandledConflictModColor : ret.Item2;
                }
            }
            else if( !StateFilter.HasFlag( ModFilter.NoConflict ) )
            {
                return ret;
            }

            ret.Item1 = true;
            SetFolderAndParentsVisible( mod.Data.SortOrder.ParentFolder );
            return ret;
        }
    }
}