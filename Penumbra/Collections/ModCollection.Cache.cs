using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Logging;
using OtterGui.Classes;
using Penumbra.GameData.ByteString;
using Penumbra.Meta.Manager;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.Collections;

public record struct ModPath( Mod Mod, FullPath Path );
public record ModConflicts( Mod Mod2, List<object> Conflicts, bool HasPriority, bool Solved );

public partial class ModCollection
{
    // The Cache contains all required temporary data to use a collection.
    // It will only be setup if a collection gets activated in any way.
    private class Cache : IDisposable
    {
        private readonly ModCollection _collection;
        private readonly SortedList<string, (SingleArray<Mod>, object?)> _changedItems = new();
        public readonly Dictionary<Utf8GamePath, ModPath> ResolvedFiles = new();
        public readonly MetaManager MetaManipulations;
        private readonly Dictionary<Mod, SingleArray<ModConflicts>> _conflicts = new();

        public IEnumerable<SingleArray<ModConflicts>> AllConflicts
            => _conflicts.Values;

        public SingleArray<ModConflicts> Conflicts( Mod mod )
            => _conflicts.TryGetValue( mod, out var c ) ? c : new SingleArray<ModConflicts>();

        // Count the number of changes of the effective file list.
        // This is used for material and imc changes.
        public int ChangeCounter { get; private set; }
        private int _changedItemsSaveCounter = -1;

        // Obtain currently changed items. Computes them if they haven't been computed before.
        public IReadOnlyDictionary<string, (SingleArray<Mod>, object?)> ChangedItems
        {
            get
            {
                SetChangedItems();
                return _changedItems;
            }
        }

        // The cache reacts through events on its collection changing.
        public Cache( ModCollection collection )
        {
            _collection = collection;
            MetaManipulations = new MetaManager( collection );
            _collection.ModSettingChanged += OnModSettingChange;
            _collection.InheritanceChanged += OnInheritanceChange;
        }

        public void Dispose()
        {
            _collection.ModSettingChanged -= OnModSettingChange;
            _collection.InheritanceChanged -= OnInheritanceChange;
        }

        // Resolve a given game path according to this collection.
        public FullPath? ResolvePath( Utf8GamePath gameResourcePath )
        {
            if( !ResolvedFiles.TryGetValue( gameResourcePath, out var candidate ) )
            {
                return null;
            }

            if( candidate.Path.InternalName.Length > Utf8GamePath.MaxGamePathLength
            || candidate.Path.IsRooted && !candidate.Path.Exists )
            {
                return null;
            }

            return candidate.Path;
        }

        public List<Utf8GamePath> ReverseResolvePath( FullPath localFilePath )
        {
            string strToSearchFor = localFilePath.FullName.Replace( '/', '\\' ).ToLower();
            return ResolvedFiles.Where( f => f.Value.Path.FullName.ToLower() ==  strToSearchFor )
                .Select( kvp => kvp.Key ).ToList();
        }

        private void OnModSettingChange( ModSettingChange type, int modIdx, int oldValue, int groupIdx, bool _ )
        {
            switch( type )
            {
                case ModSettingChange.Inheritance:
                    ReloadMod( Penumbra.ModManager[modIdx], true );
                    break;
                case ModSettingChange.EnableState:
                    if( oldValue == 0 )
                    {
                        AddMod( Penumbra.ModManager[modIdx], true );
                    }
                    else if( oldValue == 1 )
                    {
                        RemoveMod( Penumbra.ModManager[modIdx], true );
                    }
                    else if( _collection[modIdx].Settings?.Enabled == true )
                    {
                        ReloadMod( Penumbra.ModManager[modIdx], true );
                    }
                    else
                    {
                        RemoveMod( Penumbra.ModManager[modIdx], true );
                    }

                    break;
                case ModSettingChange.Priority:
                    if( Conflicts( Penumbra.ModManager[modIdx] ).Count > 0 )
                    {
                        ReloadMod( Penumbra.ModManager[modIdx], true );
                    }

                    break;
                case ModSettingChange.Setting:
                    if( _collection[modIdx].Settings?.Enabled == true )
                    {
                        ReloadMod( Penumbra.ModManager[modIdx], true );
                    }

                    break;
                case ModSettingChange.MultiInheritance:
                case ModSettingChange.MultiEnableState:
                    FullRecalculation();
                    break;
            }
        }

        // Inheritance changes are too big to check for relevance,
        // just recompute everything.
        private void OnInheritanceChange( bool _ )
            => FullRecalculation();

        public void FullRecalculation()
        {
            ResolvedFiles.Clear();
            MetaManipulations.Reset();
            _conflicts.Clear();

            // Add all forced redirects.
            Penumbra.Redirects.Apply( ResolvedFiles );

            foreach( var mod in Penumbra.ModManager )
            {
                AddMod( mod, false );
            }

            AddMetaFiles();

            ++ChangeCounter;

            if( _collection == Penumbra.CollectionManager.Default )
            {
                Penumbra.ResidentResources.Reload();
                MetaManipulations.SetFiles();
            }
        }

        public void ReloadMod( Mod mod, bool addMetaChanges )
        {
            RemoveMod( mod, addMetaChanges );
            AddMod( mod, addMetaChanges );
        }

        public void RemoveMod( Mod mod, bool addMetaChanges )
        {
            var conflicts = Conflicts( mod );

            foreach( var (path, _) in mod.AllSubMods.SelectMany( s => s.Files.Concat( s.FileSwaps ) ) )
            {
                if( !ResolvedFiles.TryGetValue( path, out var modPath ) )
                {
                    continue;
                }

                if( modPath.Mod == mod )
                {
                    ResolvedFiles.Remove( path );
                }
            }

            foreach( var manipulation in mod.AllSubMods.SelectMany( s => s.Manipulations ) )
            {
                if( MetaManipulations.TryGetValue( manipulation, out var registeredMod ) && registeredMod == mod )
                {
                    MetaManipulations.RevertMod( manipulation );
                }
            }

            _conflicts.Remove( mod );
            foreach( var conflict in conflicts )
            {
                if( conflict.HasPriority )
                {
                    ReloadMod( conflict.Mod2, false );
                }
                else
                {
                    var newConflicts = Conflicts( conflict.Mod2 ).Remove( c => c.Mod2 == mod );
                    if( newConflicts.Count > 0 )
                    {
                        _conflicts[conflict.Mod2] = newConflicts;
                    }
                    else
                    {
                        _conflicts.Remove( conflict.Mod2 );
                    }
                }
            }

            if( addMetaChanges )
            {
                ++ChangeCounter;
                if( _collection == Penumbra.CollectionManager.Default )
                {
                    Penumbra.ResidentResources.Reload();
                    MetaManipulations.SetFiles();
                }
            }
        }


        // Add all files and possibly manipulations of a given mod according to its settings in this collection.
        public void AddMod( Mod mod, bool addMetaChanges )
        {
            var settings = _collection[mod.Index].Settings;
            if( settings is not { Enabled: true } )
            {
                return;
            }

            foreach( var (group, groupIndex) in mod.Groups.WithIndex().OrderByDescending( g => g.Item1.Priority ) )
            {
                if( group.Count == 0 )
                {
                    continue;
                }

                var config = settings.Settings[groupIndex];
                switch( group.Type )
                {
                    case SelectType.Single:
                        AddSubMod( group[( int )config], mod );
                        break;
                    case SelectType.Multi:
                        {
                            foreach( var (option, _) in group.WithIndex()
                                       .OrderByDescending( p => group.OptionPriority( p.Item2 ) )
                                       .Where( p => ( ( 1 << p.Item2 ) & config ) != 0 ) )
                            {
                                AddSubMod( option, mod );
                            }

                            break;
                        }
                }
            }

            AddSubMod( mod.Default, mod );

            if( addMetaChanges )
            {
                ++ChangeCounter;
                if( mod.TotalManipulations > 0 )
                {
                    AddMetaFiles();
                }

                if( _collection == Penumbra.CollectionManager.Default )
                {
                    Penumbra.ResidentResources.Reload();
                    MetaManipulations.SetFiles();
                }
            }
        }

        // Add all files and possibly manipulations of a specific submod
        private void AddSubMod( ISubMod subMod, Mod parentMod )
        {
            foreach( var (path, file) in subMod.Files.Concat( subMod.FileSwaps ) )
            {
                // Skip all filtered files
                if( Mod.FilterFile( path ) )
                {
                    continue;
                }

                AddFile( path, file, parentMod );
            }

            foreach( var manip in subMod.Manipulations )
            {
                AddManipulation( manip, parentMod );
            }
        }

        // Add a specific file redirection, handling potential conflicts.
        // For different mods, higher mod priority takes precedence before option group priority,
        // which takes precedence before option priority, which takes precedence before ordering.
        // Inside the same mod, conflicts are not recorded.
        private void AddFile( Utf8GamePath path, FullPath file, Mod mod )
        {
            if( ResolvedFiles.TryAdd( path, new ModPath( mod, file ) ) )
            {
                return;
            }

            var modPath = ResolvedFiles[path];
            // Lower prioritized option in the same mod.
            if( mod == modPath.Mod )
            {
                return;
            }

            if( AddConflict( path, mod, modPath.Mod ) )
            {
                ResolvedFiles[path] = new ModPath( mod, file );
            }
        }


        // Remove all empty conflict sets for a given mod with the given conflicts.
        // If transitive is true, also removes the corresponding version of the other mod.
        private void RemoveEmptyConflicts( Mod mod, SingleArray<ModConflicts> oldConflicts, bool transitive )
        {
            var changedConflicts = oldConflicts.Remove( c =>
            {
                if( c.Conflicts.Count == 0 )
                {
                    if( transitive )
                    {
                        RemoveEmptyConflicts( c.Mod2, Conflicts( c.Mod2 ), false );
                    }

                    return true;
                }

                return false;
            } );
            if( changedConflicts.Count == 0 )
            {
                _conflicts.Remove( mod );
            }
            else
            {
                _conflicts[mod] = changedConflicts;
            }
        }

        // Add a new conflict between the added mod and the existing mod.
        // Update all other existing conflicts between the existing mod and other mods if necessary.
        // Returns if the added mod takes priority before the existing mod.
        private bool AddConflict( object data, Mod addedMod, Mod existingMod )
        {
            var addedPriority = addedMod.Index >= 0 ? _collection[addedMod.Index].Settings!.Priority : int.MaxValue;
            var existingPriority = existingMod.Index >= 0 ? _collection[existingMod.Index].Settings!.Priority : int.MaxValue;

            if( existingPriority < addedPriority )
            {
                var tmpConflicts = Conflicts( existingMod );
                foreach( var conflict in tmpConflicts )
                {
                    if( data is Utf8GamePath path && conflict.Conflicts.RemoveAll( p => p is Utf8GamePath x && x.Equals( path ) ) > 0
                    || data is MetaManipulation meta && conflict.Conflicts.RemoveAll( m => m is MetaManipulation x && x.Equals( meta ) ) > 0 )
                    {
                        AddConflict( data, addedMod, conflict.Mod2 );
                    }
                }

                RemoveEmptyConflicts( existingMod, tmpConflicts, true );
            }

            var addedConflicts = Conflicts( addedMod );
            var existingConflicts = Conflicts( existingMod );
            if( addedConflicts.FindFirst( c => c.Mod2 == existingMod, out var oldConflicts ) )
            {
                // Only need to change one list since both conflict lists refer to the same list.
                oldConflicts.Conflicts.Add( data );
            }
            else
            {
                // Add the same conflict list to both conflict directions.
                var conflictList = new List<object> { data };
                _conflicts[addedMod] = addedConflicts.Append( new ModConflicts( existingMod, conflictList, existingPriority < addedPriority,
                    existingPriority != addedPriority ) );
                _conflicts[existingMod] = existingConflicts.Append( new ModConflicts( addedMod, conflictList,
                    existingPriority >= addedPriority,
                    existingPriority != addedPriority ) );
            }

            return existingPriority < addedPriority;
        }

        // Add a specific manipulation, handling potential conflicts.
        // For different mods, higher mod priority takes precedence before option group priority,
        // which takes precedence before option priority, which takes precedence before ordering.
        // Inside the same mod, conflicts are not recorded.
        private void AddManipulation( MetaManipulation manip, Mod mod )
        {
            if( !MetaManipulations.TryGetValue( manip, out var existingMod ) )
            {
                MetaManipulations.ApplyMod( manip, mod );
                return;
            }

            // Lower prioritized option in the same mod.
            if( mod == existingMod )
            {
                return;
            }

            if( AddConflict( manip, mod, existingMod ) )
            {
                MetaManipulations.ApplyMod( manip, mod );
            }
        }


        // Add all necessary meta file redirects.
        private void AddMetaFiles()
            => MetaManipulations.Imc.SetFiles();

        // Identify and record all manipulated objects for this entire collection.
        private void SetChangedItems()
        {
            if( _changedItemsSaveCounter == ChangeCounter )
            {
                return;
            }

            try
            {
                _changedItemsSaveCounter = ChangeCounter;
                _changedItems.Clear();
                // Skip IMCs because they would result in far too many false-positive items,
                // since they are per set instead of per item-slot/item/variant.
                var identifier = GameData.GameData.GetIdentifier();
                foreach( var (resolved, modPath) in ResolvedFiles.Where( file => !file.Key.Path.EndsWith( 'i', 'm', 'c' ) ) )
                {
                    foreach( var (name, obj) in identifier.Identify( resolved.ToGamePath() ) )
                    {
                        if( !_changedItems.TryGetValue( name, out var data ) )
                        {
                            _changedItems.Add( name, (new SingleArray<Mod>( modPath.Mod ), obj) );
                        }
                        else if( !data.Item1.Contains( modPath.Mod ) )
                        {
                            _changedItems[name] = (data.Item1.Append( modPath.Mod ), obj is int x && data.Item2 is int y ? x + y : obj);
                        }
                        else if( obj is int x && data.Item2 is int y )
                        {
                            _changedItems[name] = (data.Item1, x + y);
                        }
                    }
                }
                // TODO: Meta Manipulations
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Unknown Error:\n{e}" );
            }
        }
    }
}