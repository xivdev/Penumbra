using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Logging;
using Penumbra.GameData.ByteString;
using Penumbra.Meta.Manager;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;

namespace Penumbra.Collections;

public partial class ModCollection
{
    // The Cache contains all required temporary data to use a collection.
    // It will only be setup if a collection gets activated in any way.
    private class Cache : IDisposable
    {
        // Shared caches to avoid allocations.
        private static readonly Dictionary< Utf8GamePath, FileRegister >     RegisteredFiles         = new(1024);
        private static readonly Dictionary< MetaManipulation, FileRegister > RegisteredManipulations = new(1024);
        private static readonly List< ModSettings? >                        ResolvedSettings        = new(128);

        private readonly ModCollection                        _collection;
        private readonly SortedList< string, object? >        _changedItems = new();
        public readonly  Dictionary< Utf8GamePath, FullPath > ResolvedFiles = new();
        public readonly  HashSet< FullPath >                  MissingFiles  = new();
        public readonly  MetaManager                          MetaManipulations;
        public           ConflictCache                        Conflicts = new();

        // Obtain currently changed items. Computes them if they haven't been computed before.
        public IReadOnlyDictionary< string, object? > ChangedItems
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
            _collection                    =  collection;
            MetaManipulations              =  new MetaManager( collection );
            _collection.ModSettingChanged  += OnModSettingChange;
            _collection.InheritanceChanged += OnInheritanceChange;
        }

        public void Dispose()
        {
            _collection.ModSettingChanged  -= OnModSettingChange;
            _collection.InheritanceChanged -= OnInheritanceChange;
        }

        // Resolve a given game path according to this collection.
        public FullPath? ResolvePath( Utf8GamePath gameResourcePath )
        {
            if( !ResolvedFiles.TryGetValue( gameResourcePath, out var candidate ) )
            {
                return null;
            }

            if( candidate.InternalName.Length > Utf8GamePath.MaxGamePathLength
            || candidate.IsRooted && !candidate.Exists )
            {
                return null;
            }

            return candidate;
        }

        private void OnModSettingChange( ModSettingChange type, int modIdx, int oldValue, int groupIdx, bool _ )
        {
            // Recompute the file list if it was not just a non-conflicting priority change
            // or a setting change for a disabled mod.
            if( type == ModSettingChange.Priority && !Conflicts.ModConflicts( modIdx ).Any()
            || type  == ModSettingChange.Setting  && !_collection[ modIdx ].Settings!.Enabled )
            {
                return;
            }

            var hasMeta = type is ModSettingChange.MultiEnableState or ModSettingChange.MultiInheritance
             || Penumbra.ModManager[ modIdx ].AllManipulations.Any();
            _collection.CalculateEffectiveFileList( hasMeta, Penumbra.CollectionManager.Default == _collection );
        }

        // Inheritance changes are too big to check for relevance,
        // just recompute everything.
        private void OnInheritanceChange( bool _ )
            => _collection.CalculateEffectiveFileList( true, true );

        // Clear all local and global caches to prepare for recomputation.
        private void ClearStorageAndPrepare()
        {
            ResolvedFiles.Clear();
            MissingFiles.Clear();
            RegisteredFiles.Clear();
            _changedItems.Clear();
            ResolvedSettings.Clear();
            Conflicts.ClearFileConflicts();
            // Obtains actual settings for this collection with all inheritances.
            ResolvedSettings.AddRange( _collection.ActualSettings );
        }

        // Recalculate all file changes from current settings. Include all fixed custom redirects.
        // Recalculate meta manipulations only if withManipulations is true.
        public void CalculateEffectiveFileList( bool withManipulations )
        {
            ClearStorageAndPrepare();
            if( withManipulations )
            {
                RegisteredManipulations.Clear();
                MetaManipulations.Reset();
            }

            AddCustomRedirects();
            for( var i = 0; i < Penumbra.ModManager.Count; ++i )
            {
                AddMod( i, withManipulations );
            }

            AddMetaFiles();
        }

        // Identify and record all manipulated objects for this entire collection.
        private void SetChangedItems()
        {
            if( _changedItems.Count > 0 || ResolvedFiles.Count + MetaManipulations.Count == 0 )
            {
                return;
            }

            try
            {
                // Skip IMCs because they would result in far too many false-positive items,
                // since they are per set instead of per item-slot/item/variant.
                var identifier = GameData.GameData.GetIdentifier();
                foreach( var resolved in ResolvedFiles.Keys.Where( file => !file.Path.EndsWith( 'i', 'm', 'c' ) ) )
                {
                    identifier.Identify( _changedItems, resolved.ToGamePath() );
                }
                // TODO: Meta Manipulations
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Unknown Error:\n{e}" );
            }
        }

        // Add a specific file redirection, handling potential conflicts.
        // For different mods, higher mod priority takes precedence before option group priority,
        // which takes precedence before option priority, which takes precedence before ordering.
        // Inside the same mod, conflicts are not recorded.
        private void AddFile( Utf8GamePath path, FullPath file, FileRegister priority )
        {
            if( RegisteredFiles.TryGetValue( path, out var register ) )
            {
                if( register.SameMod( priority, out var less ) )
                {
                    Conflicts.AddConflict( register.ModIdx, priority.ModIdx, register.ModPriority, priority.ModPriority, path );
                    if( less )
                    {
                        RegisteredFiles[ path ] = priority;
                        ResolvedFiles[ path ]   = file;
                    }
                }
                else
                {
                    // File seen before in the same mod:
                    // use higher priority or earlier recurrences in case of same priority.
                    // Do not add conflicts.
                    if( less )
                    {
                        RegisteredFiles[ path ] = priority;
                        ResolvedFiles[ path ]   = file;
                    }
                }
            }
            else // File not seen before, just add it.
            {
                RegisteredFiles.Add( path, priority );
                ResolvedFiles.Add( path, file );
            }
        }

        // Add a specific manipulation, handling potential conflicts.
        // For different mods, higher mod priority takes precedence before option group priority,
        // which takes precedence before option priority, which takes precedence before ordering.
        // Inside the same mod, conflicts are not recorded.
        private void AddManipulation( MetaManipulation manip, FileRegister priority )
        {
            if( RegisteredManipulations.TryGetValue( manip, out var register ) )
            {
                if( register.SameMod( priority, out var less ) )
                {
                    Conflicts.AddConflict( register.ModIdx, priority.ModIdx, register.ModPriority, priority.ModPriority, manip );
                    if( less )
                    {
                        RegisteredManipulations[ manip ] = priority;
                        MetaManipulations.ApplyMod( manip, priority.ModIdx );
                    }
                }
                else
                {
                    // Manipulation seen before in the same mod:
                    // use higher priority or earlier occurrences in case of same priority.
                    // Do not add conflicts.
                    if( less )
                    {
                        RegisteredManipulations[ manip ] = priority;
                        MetaManipulations.ApplyMod( manip, priority.ModIdx );
                    }
                }
            }
            else // Manipulation not seen before, just add it.
            {
                RegisteredManipulations[ manip ] = priority;
                MetaManipulations.ApplyMod( manip, priority.ModIdx );
            }
        }

        // Add all files and possibly manipulations of a specific submod with the given priorities.
        private void AddSubMod( ISubMod mod, FileRegister priority, bool withManipulations )
        {
            foreach( var (path, file) in mod.Files.Concat( mod.FileSwaps ) )
            {
                // Skip all filtered files
                if( Mod.FilterFile( path ) )
                {
                    continue;
                }

                AddFile( path, file, priority );
            }

            if( withManipulations )
            {
                foreach( var manip in mod.Manipulations )
                {
                    AddManipulation( manip, priority );
                }
            }
        }

        // Add all files and possibly manipulations of a given mod according to its settings in this collection.
        private void AddMod( int modIdx, bool withManipulations )
        {
            var settings = ResolvedSettings[ modIdx ];
            if( settings is not { Enabled: true } )
            {
                return;
            }

            var mod = Penumbra.ModManager[ modIdx ];
            AddSubMod( mod.Default, new FileRegister( modIdx, settings.Priority, 0, 0 ), withManipulations );
            for( var idx = 0; idx < mod.Groups.Count; ++idx )
            {
                var config = settings.Settings[ idx ];
                var group  = mod.Groups[ idx ];
                if( group.Count == 0 )
                {
                    continue;
                }

                switch( group.Type )
                {
                    case SelectType.Single:
                        var singlePriority = new FileRegister( modIdx, settings.Priority, group.Priority, group.Priority );
                        AddSubMod( group[ ( int )config ], singlePriority, withManipulations );
                        break;
                    case SelectType.Multi:
                    {
                        for( var optionIdx = 0; optionIdx < group.Count; ++optionIdx )
                        {
                            if( ( ( 1 << optionIdx ) & config ) != 0 )
                            {
                                var priority = new FileRegister( modIdx, settings.Priority, group.Priority, group.OptionPriority( optionIdx ) );
                                AddSubMod( group[ optionIdx ], priority, withManipulations );
                            }
                        }

                        break;
                    }
                }
            }
        }

        // Add all necessary meta file redirects.
        private void AddMetaFiles()
            => MetaManipulations.Imc.SetFiles();

        // Add all API redirects.
        private void AddCustomRedirects()
        {
            Penumbra.Redirects.Apply( ResolvedFiles );
            foreach( var gamePath in ResolvedFiles.Keys )
            {
                RegisteredFiles.Add( gamePath, new FileRegister( -1, int.MaxValue, 0, 0 ) );
            }
        }


        // Struct to keep track of all priorities involved in a mod and register and compare accordingly.
        private readonly record struct FileRegister( int ModIdx, int ModPriority, int GroupPriority, int OptionPriority )
        {
            public readonly int ModIdx         = ModIdx;
            public readonly int ModPriority    = ModPriority;
            public readonly int GroupPriority  = GroupPriority;
            public readonly int OptionPriority = OptionPriority;

            public bool SameMod( FileRegister other, out bool less )
            {
                if( ModIdx != other.ModIdx )
                {
                    less = ModPriority < other.ModPriority;
                    return true;
                }

                if( GroupPriority < other.GroupPriority )
                {
                    less = true;
                }
                else if( GroupPriority == other.GroupPriority )
                {
                    less = OptionPriority < other.OptionPriority;
                }
                else
                {
                    less = false;
                }

                return false;
            }
        };
    }
}