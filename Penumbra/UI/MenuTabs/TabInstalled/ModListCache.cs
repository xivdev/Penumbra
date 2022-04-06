using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Logging;
using OtterGui;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.UI;

public class ModListCache : IDisposable
{
    private readonly Mod.Manager _manager;

    private readonly List< FullMod >                                       _modsInOrder    = new();
    private readonly List< (bool visible, uint color) >                    _visibleMods    = new();
    private readonly Dictionary< ModFolder, (bool visible, bool enabled) > _visibleFolders = new();
    private readonly IReadOnlySet< string >                                _newMods;

    private LowerString _modFilter        = LowerString.Empty;
    private LowerString _modFilterAuthor  = LowerString.Empty;
    private LowerString _modFilterChanges = LowerString.Empty;

    private bool _listResetNecessary;
    private bool _filterResetNecessary;

    private ModFilter _stateFilter = ModFilterExtensions.UnfilteredStateMods;

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

    public ModListCache( Mod.Manager manager, IReadOnlySet< string > newMods )
    {
        _manager = manager;
        _newMods = newMods;
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

    public void RemoveMod( FullMod mod )
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
                SetFolderAndParentsVisible( _modsInOrder[ i ].Data.Order.ParentFolder );
            }
        }
    }

    public void SetTextFilter( string filter )
    {
        var lower = filter.ToLowerInvariant();
        if( lower.StartsWith( "c:" ) )
        {
            _modFilterChanges = lower[ 2.. ];
            _modFilter        = LowerString.Empty;
            _modFilterAuthor  = LowerString.Empty;
        }
        else if( lower.StartsWith( "a:" ) )
        {
            _modFilterAuthor  = lower[ 2.. ];
            _modFilter        = LowerString.Empty;
            _modFilterChanges = LowerString.Empty;
        }
        else
        {
            _modFilter        = lower;
            _modFilterAuthor  = LowerString.Empty;
            _modFilterChanges = LowerString.Empty;
        }

        ResetFilters();
    }

    private void ResetModList()
    {
        _modsInOrder.Clear();
        _visibleMods.Clear();
        _visibleFolders.Clear();

        PluginLog.Debug( "Resetting mod selector list..." );
        if( _modsInOrder.Count == 0 )
        {
            foreach( var modData in _manager.StructuredMods.AllMods( _manager.Config.SortFoldersFirst ) )
            {
                var idx = Penumbra.ModManager.Mods.IndexOf( modData );
                var mod = new FullMod( Penumbra.CollectionManager.Current[ idx ].Settings ?? ModSettings.DefaultSettings( modData.Meta ),
                    modData );
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

    public (FullMod? mod, int idx) GetModByName( string name )
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

    public (FullMod? mod, int idx) GetModByBasePath( string basePath )
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

    public (FullMod?, bool visible, uint color) GetMod( int idx )
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

    private (bool, uint) CheckFilters( FullMod mod )
    {
        var ret = ( false, 0u );

        if( _modFilter.Length > 0 && !mod.Data.Meta.Name.Contains( _modFilter ) )
        {
            return ret;
        }

        if( _modFilterAuthor.Length > 0 && !mod.Data.Meta.Author.Contains( _modFilterAuthor ) )
        {
            return ret;
        }

        if( _modFilterChanges.Length > 0 && !mod.Data.LowerChangedItemsString.Contains( _modFilterChanges ) )
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

        var isNew = _newMods.Contains( mod.Data.BasePath.Name );
        if( CheckFlags( isNew ? 1 : 0, ModFilter.IsNew, ModFilter.NotNew ) )
        {
            return ret;
        }

        if( !mod.Settings.Enabled )
        {
            if( !StateFilter.HasFlag( ModFilter.Disabled ) || !StateFilter.HasFlag( ModFilter.NoConflict ) )
            {
                return ret;
            }

            ret.Item2 = ret.Item2 == 0 ? Colors.DisabledModColor : ret.Item2;
        }

        if( mod.Settings.Enabled && !StateFilter.HasFlag( ModFilter.Enabled ) )
        {
            return ret;
        }

        var conflicts = Penumbra.CollectionManager.Current.ModConflicts( mod.Data.Index ).ToList();
        if( conflicts.Count > 0 )
        {
            if( conflicts.Any( c => !c.Solved ) )
            {
                if( !StateFilter.HasFlag( ModFilter.UnsolvedConflict ) )
                {
                    return ret;
                }

                ret.Item2 = ret.Item2 == 0 ? Colors.ConflictingModColor : ret.Item2;
            }
            else
            {
                if( !StateFilter.HasFlag( ModFilter.SolvedConflict ) )
                {
                    return ret;
                }

                ret.Item2 = ret.Item2 == 0 ? Colors.HandledConflictModColor : ret.Item2;
            }
        }
        else if( !StateFilter.HasFlag( ModFilter.NoConflict ) )
        {
            return ret;
        }

        ret.Item1 = true;
        if( isNew )
        {
            ret.Item2 = Colors.NewModColor;
        }

        SetFolderAndParentsVisible( mod.Data.Order.ParentFolder );
        return ret;
    }
}