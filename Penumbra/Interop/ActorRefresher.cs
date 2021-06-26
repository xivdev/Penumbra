using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Plugin;
using Penumbra.Mods;

namespace Penumbra.Interop
{
    public enum Redraw
    {
        WithoutSettings,
        WithSettings,
        OnlyWithSettings,
        Unload,
        RedrawWithoutSettings,
        RedrawWithSettings,
    }

    public class ActorRefresher : IDisposable
    {
        private const int RenderModeOffset      = 0x0104;
        private const int ModelInvisibilityFlag = 0b10;
        private const int ModelIsLoadingFlag    = 0x800;
        private const int UnloadAllRedrawDelay  = 250;
        private const int NpcActorId            = -536870912;

        private readonly DalamudPluginInterface                        _pi;
        private readonly ModManager                                    _mods;
        private readonly Queue< (int actorId, string name, Redraw s) > _actorIds = new();

        private int     _currentFrame       = 0;
        private bool    _changedSettings    = false;
        private int     _currentActorId     = -1;
        private string? _currentActorName   = null;
        private Redraw  _currentActorRedraw = Redraw.Unload;

        public ActorRefresher( DalamudPluginInterface pi, ModManager mods )
        {
            _pi   = pi;
            _mods = mods;
        }

        private void ChangeSettings()
        {
            if( _currentActorName != null && _mods.Collections.CharacterCollection.TryGetValue( _currentActorName, out var collection ) )
            {
                _changedSettings                   = true;
                _mods.Collections.ActiveCollection = collection;
            }
        }

        private void RestoreSettings()
        {
            _mods.Collections.ActiveCollection = _mods.Collections.CurrentCollection;
            _changedSettings                   = false;
        }

        private static unsafe void WriteInvisible( IntPtr renderPtr )
        {
            if( renderPtr != IntPtr.Zero )
            {
                *( int* )renderPtr |= ModelInvisibilityFlag;
            }
        }

        private static unsafe bool StillLoading( IntPtr renderPtr )
        {
            if( renderPtr != IntPtr.Zero )
            {
                return *( int* )renderPtr != 0;
            }

            return false;
        }

        private static unsafe void WriteVisible( IntPtr renderPtr )
        {
            if( renderPtr != IntPtr.Zero )
            {
                *( int* )renderPtr &= ~ModelInvisibilityFlag;
            }
        }

        private bool CheckActor( Actor actor )
        {
            if( _currentActorId != actor.ActorId )
            {
                return false;
            }

            if( _currentActorId != NpcActorId )
            {
                return true;
            }

            return _currentActorName == actor.Name;
        }

        private Actor? FindCurrentActor()
            => _pi.ClientState.Actors.FirstOrDefault( CheckActor );

        private void PopActor()
        {
            if( _actorIds.Count > 0 )
            {
                var (id, name, s)   = _actorIds.Dequeue();
                _currentActorName   = name;
                _currentActorId     = id;
                _currentActorRedraw = s;
                var actor = FindCurrentActor();
                if( actor == null )
                {
                    return;
                }

                ++_currentFrame;
            }
            else
            {
                _pi.Framework.OnUpdateEvent -= OnUpdateEvent;
            }
        }

        private void ApplySettingsOrRedraw()
        {
            var actor = FindCurrentActor();
            if( actor == null )
            {
                return;
            }

            if( StillLoading( actor.Address + RenderModeOffset ) )
            {
                return;
            }

            switch( _currentActorRedraw )
            {
                case Redraw.Unload:
                    WriteInvisible( actor.Address + RenderModeOffset );
                    _currentFrame = 0;
                    break;
                case Redraw.RedrawWithSettings:
                    ChangeSettings();
                    ++_currentFrame;
                    break;
                case Redraw.RedrawWithoutSettings:
                    WriteVisible( actor.Address + RenderModeOffset );
                    _currentFrame = 0;
                    break;
                case Redraw.WithoutSettings:
                    WriteInvisible( actor.Address + RenderModeOffset );
                    ++_currentFrame;
                    break;
                case Redraw.WithSettings:
                    ChangeSettings();
                    WriteInvisible( actor.Address + RenderModeOffset );
                    ++_currentFrame;
                    break;
                case Redraw.OnlyWithSettings:
                    ChangeSettings();
                    if( !_changedSettings )
                    {
                        return;
                    }

                    WriteInvisible( actor.Address + RenderModeOffset );
                    ++_currentFrame;
                    break;
                default: throw new InvalidEnumArgumentException();
            }
        }

        private void StartRedrawAndWait()
        {
            var actor = FindCurrentActor();
            if( actor == null )
            {
                RevertSettings();
                return;
            }

            WriteVisible( actor.Address + RenderModeOffset );
            _currentFrame = _changedSettings ? _currentFrame + 1 : 0;
        }

        private void RevertSettings()
        {
            var actor = FindCurrentActor();
            if( actor != null )
            {
                if( !StillLoading( actor.Address + RenderModeOffset ) )
                {
                    RestoreSettings();
                    _currentFrame = 0;
                }
            }
            else
            {
                _currentFrame = 0;
            }
        }

        private void OnUpdateEvent( object framework )
        {
            switch( _currentFrame )
            {
                case 0:
                    PopActor();
                    break;
                case 1:
                    ApplySettingsOrRedraw();
                    break;
                case 2:
                    StartRedrawAndWait();
                    break;
                case 3:
                    RevertSettings();
                    break;
                default:
                    _currentFrame = 0;
                    break;
            }
        }

        private void RedrawActorIntern( int actorId, string actorName, Redraw settings )
        {
            if( _actorIds.Contains( ( actorId, actorName, settings ) ) )
            {
                return;
            }

            _actorIds.Enqueue( ( actorId, actorName, settings ) );
            if( _actorIds.Count == 1 )
            {
                _pi.Framework.OnUpdateEvent += OnUpdateEvent;
            }
        }

        public void RedrawActor( Actor? actor, Redraw settings = Redraw.WithSettings )
        {
            if( actor != null )
            {
                RedrawActorIntern( actor.ActorId, actor.Name, settings );
            }
        }

        private Actor? GetName( string name )
        {
            var lowerName = name.ToLowerInvariant();
            return lowerName switch
            {
                ""          => null,
                "<me>"      => _pi.ClientState.Actors[ 0 ],
                "self"      => _pi.ClientState.Actors[ 0 ],
                "<t>"       => _pi.ClientState.Targets.CurrentTarget,
                "target"    => _pi.ClientState.Targets.CurrentTarget,
                "<f>"       => _pi.ClientState.Targets.FocusTarget,
                "focus"     => _pi.ClientState.Targets.FocusTarget,
                "<mo>"      => _pi.ClientState.Targets.MouseOverTarget,
                "mouseover" => _pi.ClientState.Targets.MouseOverTarget,
                _ => _pi.ClientState.Actors.FirstOrDefault(
                    a => string.Equals( a.Name, lowerName, StringComparison.InvariantCultureIgnoreCase ) ),
            };
        }

        public void RedrawActor( string name, Redraw settings = Redraw.WithSettings )
            => RedrawActor( GetName( name ), settings );

        public void RedrawAll( Redraw settings = Redraw.WithSettings )
        {
            foreach( var actor in _pi.ClientState.Actors )
            {
                RedrawActor( actor, settings );
            }
        }

        private void UnloadAll()
        {
            foreach( var a in _pi.ClientState.Actors )
            {
                WriteInvisible( a.Address + RenderModeOffset );
            }
        }

        private void RedrawAllWithoutSettings()
        {
            foreach( var a in _pi.ClientState.Actors )
            {
                WriteVisible( a.Address + RenderModeOffset );
            }
        }

        public async void UnloadAtOnceRedrawWithSettings()
        {
            UnloadAll();
            await Task.Delay( UnloadAllRedrawDelay );
            RedrawAll( Redraw.RedrawWithSettings );
        }

        public async void UnloadAtOnceRedrawWithoutSettings()
        {
            UnloadAll();
            await Task.Delay( UnloadAllRedrawDelay );
            RedrawAllWithoutSettings();
        }

        public void Dispose()
        {
            RevertSettings();
            _actorIds.Clear();
            _pi.Framework.OnUpdateEvent -= OnUpdateEvent;
        }
    }
}