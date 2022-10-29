using System;
using System.Collections.Generic;
using System.Linq;
using OtterGui;
using OtterGui.Filesystem;
using Penumbra.Api.Enums;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;
using Penumbra.Util;

namespace Penumbra.Mods;

public sealed partial class Mod
{
    public sealed partial class Manager
    {
        public delegate void ModOptionChangeDelegate( ModOptionChangeType type, Mod mod, int groupIdx, int optionIdx, int movedToIdx );
        public event ModOptionChangeDelegate ModOptionChanged;

        public void ChangeModGroupType( Mod mod, int groupIdx, GroupType type )
        {
            var group = mod._groups[ groupIdx ];
            if( group.Type == type )
            {
                return;
            }

            mod._groups[ groupIdx ] = group.Convert( type );
            ModOptionChanged.Invoke( ModOptionChangeType.GroupTypeChanged, mod, groupIdx, -1, -1 );
        }

        public void ChangeModGroupDefaultOption( Mod mod, int groupIdx, uint defaultOption )
        {
            var group = mod._groups[groupIdx];
            if( group.DefaultSettings == defaultOption )
            {
                return;
            }

            group.DefaultSettings = defaultOption;
            ModOptionChanged.Invoke( ModOptionChangeType.DefaultOptionChanged, mod, groupIdx, -1, -1 );
        }

        public void RenameModGroup( Mod mod, int groupIdx, string newName )
        {
            var group   = mod._groups[ groupIdx ];
            var oldName = group.Name;
            if( oldName == newName || !VerifyFileName( mod, group, newName, true ) )
            {
                return;
            }

            group.DeleteFile( mod.ModPath, groupIdx );

            var _ = group switch
            {
                SingleModGroup s => s.Name = newName,
                MultiModGroup m  => m.Name = newName,
                _                => newName,
            };

            ModOptionChanged.Invoke( ModOptionChangeType.GroupRenamed, mod, groupIdx, -1, -1 );
        }

        public void AddModGroup( Mod mod, GroupType type, string newName )
        {
            if( !VerifyFileName( mod, null, newName, true ) )
            {
                return;
            }

            var maxPriority = mod._groups.Count == 0 ? 0 : mod._groups.Max( o => o.Priority ) + 1;

            mod._groups.Add( type == GroupType.Multi
                ? new MultiModGroup { Name  = newName, Priority = maxPriority }
                : new SingleModGroup { Name = newName, Priority = maxPriority } );
            ModOptionChanged.Invoke( ModOptionChangeType.GroupAdded, mod, mod._groups.Count - 1, -1, -1 );
        }

        public void DeleteModGroup( Mod mod, int groupIdx )
        {
            var group = mod._groups[ groupIdx ];
            ModOptionChanged.Invoke( ModOptionChangeType.PrepareChange, mod, groupIdx, -1, -1 );
            mod._groups.RemoveAt( groupIdx );
            UpdateSubModPositions( mod, groupIdx );
            group.DeleteFile( mod.ModPath, groupIdx );
            ModOptionChanged.Invoke( ModOptionChangeType.GroupDeleted, mod, groupIdx, -1, -1 );
        }

        public void MoveModGroup( Mod mod, int groupIdxFrom, int groupIdxTo )
        {
            if( mod._groups.Move( groupIdxFrom, groupIdxTo ) )
            {
                UpdateSubModPositions( mod, Math.Min( groupIdxFrom, groupIdxTo ) );
                ModOptionChanged.Invoke( ModOptionChangeType.GroupMoved, mod, groupIdxFrom, -1, groupIdxTo );
            }
        }

        private static void UpdateSubModPositions( Mod mod, int fromGroup )
        {
            foreach( var (group, groupIdx) in mod._groups.WithIndex().Skip( fromGroup ) )
            {
                foreach( var (o, optionIdx) in group.OfType< SubMod >().WithIndex() )
                {
                    o.SetPosition( groupIdx, optionIdx );
                }
            }
        }

        public void ChangeGroupDescription( Mod mod, int groupIdx, string newDescription )
        {
            var group = mod._groups[ groupIdx ];
            if( group.Description == newDescription )
            {
                return;
            }

            var _ = group switch
            {
                SingleModGroup s => s.Description = newDescription,
                MultiModGroup m  => m.Description = newDescription,
                _                => newDescription,
            };
            ModOptionChanged.Invoke( ModOptionChangeType.DisplayChange, mod, groupIdx, -1, -1 );
        }

        public void ChangeGroupPriority( Mod mod, int groupIdx, int newPriority )
        {
            var group = mod._groups[ groupIdx ];
            if( group.Priority == newPriority )
            {
                return;
            }

            var _ = group switch
            {
                SingleModGroup s => s.Priority = newPriority,
                MultiModGroup m  => m.Priority = newPriority,
                _                => newPriority,
            };
            ModOptionChanged.Invoke( ModOptionChangeType.PriorityChanged, mod, groupIdx, -1, -1 );
        }

        public void ChangeOptionPriority( Mod mod, int groupIdx, int optionIdx, int newPriority )
        {
            switch( mod._groups[ groupIdx ] )
            {
                case SingleModGroup:
                    ChangeGroupPriority( mod, groupIdx, newPriority );
                    break;
                case MultiModGroup m:
                    if( m.PrioritizedOptions[ optionIdx ].Priority == newPriority )
                    {
                        return;
                    }

                    m.PrioritizedOptions[ optionIdx ] = ( m.PrioritizedOptions[ optionIdx ].Mod, newPriority );
                    ModOptionChanged.Invoke( ModOptionChangeType.PriorityChanged, mod, groupIdx, optionIdx, -1 );
                    return;
            }
        }

        public void RenameOption( Mod mod, int groupIdx, int optionIdx, string newName )
        {
            switch( mod._groups[ groupIdx ] )
            {
                case SingleModGroup s:
                    if( s.OptionData[ optionIdx ].Name == newName )
                    {
                        return;
                    }

                    s.OptionData[ optionIdx ].Name = newName;
                    break;
                case MultiModGroup m:
                    var option = m.PrioritizedOptions[ optionIdx ].Mod;
                    if( option.Name == newName )
                    {
                        return;
                    }

                    option.Name = newName;
                    break;
            }

            ModOptionChanged.Invoke( ModOptionChangeType.DisplayChange, mod, groupIdx, optionIdx, -1 );
        }

        public void AddOption( Mod mod, int groupIdx, string newName )
        {
            var group  = mod._groups[ groupIdx ];
            var subMod = new SubMod( mod ) { Name = newName };
            subMod.SetPosition( groupIdx, group.Count );
            switch( group )
            {
                case SingleModGroup s:
                    s.OptionData.Add( subMod );
                    break;
                case MultiModGroup m:
                    m.PrioritizedOptions.Add( ( subMod, 0 ) );
                    break;
            }

            ModOptionChanged.Invoke( ModOptionChangeType.OptionAdded, mod, groupIdx, group.Count - 1, -1 );
        }

        public void AddOption( Mod mod, int groupIdx, ISubMod option, int priority = 0 )
        {
            if( option is not SubMod o )
            {
                return;
            }

            var group = mod._groups[ groupIdx ];
            if( group.Count > 63 )
            {
                Penumbra.Log.Error(
                    $"Could not add option {option.Name} to {group.Name} for mod {mod.Name}, "
                  + "since only up to 64 options are supported in one group." );
                return;
            }

            o.SetPosition( groupIdx, group.Count );

            switch( group )
            {
                case SingleModGroup s:
                    s.OptionData.Add( o );
                    break;
                case MultiModGroup m:
                    m.PrioritizedOptions.Add( ( o, priority ) );
                    break;
            }

            ModOptionChanged.Invoke( ModOptionChangeType.OptionAdded, mod, groupIdx, group.Count - 1, -1 );
        }

        public void DeleteOption( Mod mod, int groupIdx, int optionIdx )
        {
            var group = mod._groups[ groupIdx ];
            ModOptionChanged.Invoke( ModOptionChangeType.PrepareChange, mod, groupIdx, optionIdx, -1 );
            switch( group )
            {
                case SingleModGroup s:
                    s.OptionData.RemoveAt( optionIdx );

                    break;
                case MultiModGroup m:
                    m.PrioritizedOptions.RemoveAt( optionIdx );
                    break;
            }

            group.UpdatePositions( optionIdx );
            ModOptionChanged.Invoke( ModOptionChangeType.OptionDeleted, mod, groupIdx, optionIdx, -1 );
        }

        public void MoveOption( Mod mod, int groupIdx, int optionIdxFrom, int optionIdxTo )
        {
            var group = mod._groups[ groupIdx ];
            if( group.MoveOption( optionIdxFrom, optionIdxTo ) )
            {
                ModOptionChanged.Invoke( ModOptionChangeType.OptionMoved, mod, groupIdx, optionIdxFrom, optionIdxTo );
            }
        }

        public void OptionSetManipulations( Mod mod, int groupIdx, int optionIdx, HashSet< MetaManipulation > manipulations )
        {
            var subMod = GetSubMod( mod, groupIdx, optionIdx );
            if( subMod.Manipulations.Count == manipulations.Count
            && subMod.Manipulations.All( m => manipulations.TryGetValue( m, out var old ) && old.EntryEquals( m ) ) )
            {
                return;
            }

            ModOptionChanged.Invoke( ModOptionChangeType.PrepareChange, mod, groupIdx, optionIdx, -1 );
            subMod.ManipulationData = manipulations;
            ModOptionChanged.Invoke( ModOptionChangeType.OptionMetaChanged, mod, groupIdx, optionIdx, -1 );
        }

        public void OptionSetFiles( Mod mod, int groupIdx, int optionIdx, Dictionary< Utf8GamePath, FullPath > replacements )
        {
            var subMod = GetSubMod( mod, groupIdx, optionIdx );
            if( subMod.FileData.SetEquals( replacements ) )
            {
                return;
            }

            ModOptionChanged.Invoke( ModOptionChangeType.PrepareChange, mod, groupIdx, optionIdx, -1 );
            subMod.FileData = replacements;
            ModOptionChanged.Invoke( ModOptionChangeType.OptionFilesChanged, mod, groupIdx, optionIdx, -1 );
        }

        public void OptionAddFiles( Mod mod, int groupIdx, int optionIdx, Dictionary< Utf8GamePath, FullPath > additions )
        {
            var subMod   = GetSubMod( mod, groupIdx, optionIdx );
            var oldCount = subMod.FileData.Count;
            subMod.FileData.AddFrom( additions );
            if( oldCount != subMod.FileData.Count )
            {
                ModOptionChanged.Invoke( ModOptionChangeType.OptionFilesAdded, mod, groupIdx, optionIdx, -1 );
            }
        }

        public void OptionSetFileSwaps( Mod mod, int groupIdx, int optionIdx, Dictionary< Utf8GamePath, FullPath > swaps )
        {
            var subMod = GetSubMod( mod, groupIdx, optionIdx );
            if( subMod.FileSwapData.SetEquals( swaps ) )
            {
                return;
            }

            ModOptionChanged.Invoke( ModOptionChangeType.PrepareChange, mod, groupIdx, optionIdx, -1 );
            subMod.FileSwapData = swaps;
            ModOptionChanged.Invoke( ModOptionChangeType.OptionSwapsChanged, mod, groupIdx, optionIdx, -1 );
        }

        public static bool VerifyFileName( Mod mod, IModGroup? group, string newName, bool message )
        {
            var path = newName.RemoveInvalidPathSymbols();
            if( path.Length == 0
            || mod.Groups.Any( o => !ReferenceEquals( o, group )
                && string.Equals( o.Name.RemoveInvalidPathSymbols(), path, StringComparison.OrdinalIgnoreCase ) ) )
            {
                if( message )
                {
                    Penumbra.Log.Warning( $"Could not name option {newName} because option with same filename {path} already exists." );
                }

                return false;
            }

            return true;
        }

        private static SubMod GetSubMod( Mod mod, int groupIdx, int optionIdx )
        {
            if( groupIdx == -1 && optionIdx == 0 )
            {
                return mod._default;
            }

            return mod._groups[ groupIdx ] switch
            {
                SingleModGroup s => s.OptionData[ optionIdx ],
                MultiModGroup m  => m.PrioritizedOptions[ optionIdx ].Mod,
                _                => throw new InvalidOperationException(),
            };
        }

        private static void OnModOptionChange( ModOptionChangeType type, Mod mod, int groupIdx, int _, int _2 )
        {
            if( type == ModOptionChangeType.PrepareChange )
            {
                return;
            }

            // File deletion is handled in the actual function.
            if( type is ModOptionChangeType.GroupDeleted or ModOptionChangeType.GroupMoved )
            {
                mod.SaveAllGroups();
            }
            else
            {
                if( groupIdx == -1 )
                {
                    mod.SaveDefaultModDelayed();
                }
                else
                {
                    IModGroup.SaveDelayed( mod._groups[ groupIdx ], mod.ModPath, groupIdx );
                }
            }

            bool ComputeChangedItems()
            {
                mod.ComputeChangedItems();
                return true;
            }

            // State can not change on adding groups, as they have no immediate options.
            var unused = type switch
            {
                ModOptionChangeType.GroupAdded       => ComputeChangedItems() & mod.SetCounts(),
                ModOptionChangeType.GroupDeleted     => ComputeChangedItems() & mod.SetCounts(),
                ModOptionChangeType.GroupMoved       => false,
                ModOptionChangeType.GroupTypeChanged => mod.HasOptions = mod.Groups.Any( o => o.IsOption ),
                ModOptionChangeType.PriorityChanged  => false,
                ModOptionChangeType.OptionAdded      => ComputeChangedItems() & mod.SetCounts(),
                ModOptionChangeType.OptionDeleted    => ComputeChangedItems() & mod.SetCounts(),
                ModOptionChangeType.OptionMoved      => false,
                ModOptionChangeType.OptionFilesChanged => ComputeChangedItems()
                  & ( 0 < ( mod.TotalFileCount = mod.AllSubMods.Sum( s => s.Files.Count ) ) ),
                ModOptionChangeType.OptionSwapsChanged => ComputeChangedItems()
                  & ( 0 < ( mod.TotalSwapCount = mod.AllSubMods.Sum( s => s.FileSwaps.Count ) ) ),
                ModOptionChangeType.OptionMetaChanged => ComputeChangedItems()
                  & ( 0 < ( mod.TotalManipulations = mod.AllSubMods.Sum( s => s.Manipulations.Count ) ) ),
                ModOptionChangeType.DisplayChange => false,
                _                                 => false,
            };
        }
    }
}