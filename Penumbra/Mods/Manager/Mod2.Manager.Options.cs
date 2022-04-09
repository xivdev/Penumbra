using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Logging;
using Penumbra.GameData.ByteString;
using Penumbra.Meta.Manipulations;
using Penumbra.Util;

namespace Penumbra.Mods;

public enum ModOptionChangeType
{
    GroupRenamed,
    GroupAdded,
    GroupDeleted,
    PriorityChanged,
    OptionAdded,
    OptionDeleted,
    OptionChanged,
    DisplayChange,
}

public sealed partial class Mod2
{
    public sealed partial class Manager
    {
        public delegate void ModOptionChangeDelegate( ModOptionChangeType type, Mod2 mod, int groupIdx, int optionIdx );
        public event ModOptionChangeDelegate ModOptionChanged;

        public void RenameModGroup( Mod2 mod, int groupIdx, string newName )
        {
            var group   = mod._groups[ groupIdx ];
            var oldName = group.Name;
            if( oldName == newName || !VerifyFileName( mod, group, newName ) )
            {
                return;
            }

            var _ = group switch
            {
                SingleModGroup s => s.Name = newName,
                MultiModGroup m  => m.Name = newName,
                _                => newName,
            };

            ModOptionChanged.Invoke( ModOptionChangeType.GroupRenamed, mod, groupIdx, 0 );
        }

        public void AddModGroup( Mod2 mod, SelectType type, string newName )
        {
            if( !VerifyFileName( mod, null, newName ) )
            {
                return;
            }

            var maxPriority = mod._groups.Max( o => o.Priority ) + 1;

            mod._groups.Add( type == SelectType.Multi
                ? new MultiModGroup { Name  = newName, Priority = maxPriority }
                : new SingleModGroup { Name = newName, Priority = maxPriority } );
            ModOptionChanged.Invoke( ModOptionChangeType.GroupAdded, mod, mod._groups.Count - 1, 0 );
        }

        public void DeleteModGroup( Mod2 mod, int groupIdx )
        {
            var group = mod._groups[ groupIdx ];
            mod._groups.RemoveAt( groupIdx );
            group.DeleteFile( BasePath );
            ModOptionChanged.Invoke( ModOptionChangeType.GroupDeleted, mod, groupIdx, 0 );
        }

        public void ChangeGroupDescription( Mod2 mod, int groupIdx, string newDescription )
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
            ModOptionChanged.Invoke( ModOptionChangeType.DisplayChange, mod, groupIdx, 0 );
        }

        public void ChangeGroupPriority( Mod2 mod, int groupIdx, int newPriority )
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
            ModOptionChanged.Invoke( ModOptionChangeType.PriorityChanged, mod, groupIdx, -1 );
        }

        public void ChangeOptionPriority( Mod2 mod, int groupIdx, int optionIdx, int newPriority )
        {
            switch( mod._groups[ groupIdx ] )
            {
                case SingleModGroup s:
                    ChangeGroupPriority( mod, groupIdx, newPriority );
                    break;
                case MultiModGroup m:
                    if( m.PrioritizedOptions[ optionIdx ].Priority == newPriority )
                    {
                        return;
                    }

                    m.PrioritizedOptions[ optionIdx ] = ( m.PrioritizedOptions[ optionIdx ].Mod, newPriority );
                    ModOptionChanged.Invoke( ModOptionChangeType.PriorityChanged, mod, groupIdx, optionIdx );
                    return;
            }
        }

        public void RenameOption( Mod2 mod, int groupIdx, int optionIdx, string newName )
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
                    return;
            }

            ModOptionChanged.Invoke( ModOptionChangeType.DisplayChange, mod, groupIdx, optionIdx );
        }

        public void AddOption( Mod2 mod, int groupIdx, string newName )
        {
            switch( mod._groups[ groupIdx ] )
            {
                case SingleModGroup s:
                    s.OptionData.Add( new SubMod { Name = newName } );
                    break;
                case MultiModGroup m:
                    m.PrioritizedOptions.Add( ( new SubMod { Name = newName }, 0 ) );
                    break;
            }

            ModOptionChanged.Invoke( ModOptionChangeType.OptionAdded, mod, groupIdx, mod._groups[ groupIdx ].Count - 1 );
        }

        public void DeleteOption( Mod2 mod, int groupIdx, int optionIdx )
        {
            switch( mod._groups[ groupIdx ] )
            {
                case SingleModGroup s:
                    s.OptionData.RemoveAt( optionIdx );
                    break;
                case MultiModGroup m:
                    m.PrioritizedOptions.RemoveAt( optionIdx );
                    break;
            }

            ModOptionChanged.Invoke( ModOptionChangeType.OptionDeleted, mod, groupIdx, optionIdx );
        }

        public void OptionSetManipulation( Mod2 mod, int groupIdx, int optionIdx, MetaManipulation manip, bool delete = false )
        {
            var subMod = GetSubMod( mod, groupIdx, optionIdx );
            if( delete )
            {
                if( !subMod.ManipulationData.Remove( manip ) )
                {
                    return;
                }
            }
            else
            {
                if( subMod.ManipulationData.TryGetValue( manip, out var oldManip ) )
                {
                    if( manip.EntryEquals( oldManip ) )
                    {
                        return;
                    }

                    subMod.ManipulationData.Remove( oldManip );
                    subMod.ManipulationData.Add( manip );
                }
                else
                {
                    subMod.ManipulationData.Add( manip );
                }
            }

            ModOptionChanged.Invoke( ModOptionChangeType.OptionChanged, mod, groupIdx, optionIdx );
        }

        public void OptionSetFile( Mod2 mod, int groupIdx, int optionIdx, Utf8GamePath gamePath, FullPath? newPath )
        {
            var subMod = GetSubMod( mod, groupIdx, optionIdx );
            if( OptionSetFile( subMod.FileData, gamePath, newPath ) )
            {
                ModOptionChanged.Invoke( ModOptionChangeType.OptionChanged, mod, groupIdx, optionIdx );
            }
        }

        public void OptionSetFileSwap( Mod2 mod, int groupIdx, int optionIdx, Utf8GamePath gamePath, FullPath? newPath )
        {
            var subMod = GetSubMod( mod, groupIdx, optionIdx );
            if( OptionSetFile( subMod.FileSwapData, gamePath, newPath ) )
            {
                ModOptionChanged.Invoke( ModOptionChangeType.OptionChanged, mod, groupIdx, optionIdx );
            }
        }

        private bool VerifyFileName( Mod2 mod, IModGroup? group, string newName )
        {
            var path = newName.RemoveInvalidPathSymbols();
            if( mod.Groups.Any( o => !ReferenceEquals( o, group )
                && string.Equals( o.Name.RemoveInvalidPathSymbols(), path, StringComparison.InvariantCultureIgnoreCase ) ) )
            {
                PluginLog.Warning( $"Could not name option {newName} because option with same filename {path} already exists." );
                return false;
            }

            return true;
        }

        private static SubMod GetSubMod( Mod2 mod, int groupIdx, int optionIdx )
        {
            return mod._groups[ groupIdx ] switch
            {
                SingleModGroup s => s.OptionData[ optionIdx ],
                MultiModGroup m  => m.PrioritizedOptions[ optionIdx ].Mod,
                _                => throw new InvalidOperationException(),
            };
        }

        private static bool OptionSetFile( IDictionary< Utf8GamePath, FullPath > dict, Utf8GamePath gamePath, FullPath? newPath )
        {
            if( dict.TryGetValue( gamePath, out var oldPath ) )
            {
                if( newPath == null )
                {
                    dict.Remove( gamePath );
                    return true;
                }

                if( newPath.Value.Equals( oldPath ) )
                {
                    return false;
                }

                dict[ gamePath ] = newPath.Value;
                return true;
            }

            if( newPath == null )
            {
                return false;
            }

            dict.Add( gamePath, newPath.Value );
            return true;
        }

        private static void OnModOptionChange( ModOptionChangeType type, Mod2 mod, int groupIdx, int _ )
        {
            // File deletion is handled in the actual function.
            if( type != ModOptionChangeType.GroupDeleted )
            {
                IModGroup.SaveModGroup( mod._groups[ groupIdx ], mod.BasePath );
            }

            // State can not change on adding groups, as they have no immediate options.
            mod.HasOptions = type switch
            {
                ModOptionChangeType.GroupDeleted  => mod.HasOptions =  mod.Groups.Any( o => o.IsOption ),
                ModOptionChangeType.OptionAdded   => mod.HasOptions |= mod._groups[ groupIdx ].IsOption,
                ModOptionChangeType.OptionDeleted => mod.HasOptions =  mod.Groups.Any( o => o.IsOption ),
                _                                 => mod.HasOptions,
            };
        }
    }
}