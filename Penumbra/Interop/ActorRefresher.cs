using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Plugin;
using Penumbra.Api;
using Penumbra.Mods;

namespace Penumbra.Interop
{
    public class ActorRefresher : IDisposable
    {
        private delegate void ManipulateDraw( IntPtr actor );

        [Flags]
        public enum LoadingFlags : int
        {
            Invisibility      = 0x00_00_00_02,
            IsLoading         = 0x00_00_08_00,
            SomeNpcFlag       = 0x00_00_01_00,
            MaybeCulled       = 0x00_00_04_00,
            MaybeHiddenMinion = 0x00_00_80_00,
            MaybeHiddenSummon = 0x00_80_00_00,
        }

        private const int RenderModeOffset     = 0x0104;
        private const int UnloadAllRedrawDelay = 250;
        private const int NpcActorId           = -536870912;
        public const  int GPosePlayerActorIdx  = 201;

        private readonly DalamudPluginInterface                            _pi;
        private readonly ModManager                                        _mods;
        private readonly Queue< (int actorId, string name, RedrawType s) > _actorIds = new();

        private int          _currentFrame           = 0;
        private bool         _changedSettings        = false;
        private int          _currentActorId         = -1;
        private string?      _currentActorName       = null;
        private LoadingFlags _currentActorStartState = 0;
        private RedrawType   _currentActorRedrawType = RedrawType.Unload;

        public static IntPtr RenderPtr( Actor actor )
            => actor.Address + RenderModeOffset;

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
            _mods.Collections.ActiveCollection = _mods.Collections.DefaultCollection;
            _changedSettings                   = false;
        }

        private unsafe void WriteInvisible( Actor actor, int actorIdx )
        {
            var renderPtr = RenderPtr( actor );
            if( renderPtr == IntPtr.Zero )
            {
                return;
            }

            _currentActorStartState     =  *( LoadingFlags* )renderPtr;
            *( LoadingFlags* )renderPtr |= LoadingFlags.Invisibility;

            if( actorIdx == GPosePlayerActorIdx )
            {
                var ptr         = ( void*** )actor.Address;
                var disableDraw = Marshal.GetDelegateForFunctionPointer< ManipulateDraw >( new IntPtr( ptr[ 0 ][ 17 ] ) );
                disableDraw( actor.Address );
            }
        }

        private unsafe bool StillLoading( IntPtr renderPtr )
        {
            const LoadingFlags stillLoadingFlags = LoadingFlags.SomeNpcFlag
              | LoadingFlags.MaybeCulled
              | LoadingFlags.MaybeHiddenMinion
              | LoadingFlags.MaybeHiddenSummon;

            if( renderPtr != IntPtr.Zero )
            {
                var loadingFlags = *( LoadingFlags* )renderPtr;
                if( loadingFlags == _currentActorStartState )
                {
                    return false;
                }

                return !( loadingFlags == 0 || ( loadingFlags & stillLoadingFlags ) != 0 );
            }

            return false;
        }

        private static unsafe void WriteVisible( Actor actor, int actorIdx )
        {
            var renderPtr = RenderPtr( actor );
            *( LoadingFlags* )renderPtr &= ~LoadingFlags.Invisibility;

            if( actorIdx == GPosePlayerActorIdx )
            {
                var ptr        = ( void*** )actor.Address;
                var enableDraw = Marshal.GetDelegateForFunctionPointer< ManipulateDraw >( new IntPtr( ptr[ 0 ][ 16 ] ) );
                enableDraw( actor.Address );
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

        private (Actor?, int) FindCurrentActor()
        {
            for( var i = 0; i < _pi.ClientState.Actors.Length; ++i )
            {
                var actor = _pi.ClientState.Actors[ i ];
                if( actor != null && CheckActor( actor ) )
                {
                    return ( actor, i );
                }
            }

            return ( null, -1 );
        }

        private void PopActor()
        {
            if( _actorIds.Count > 0 )
            {
                var (id, name, s)       = _actorIds.Dequeue();
                _currentActorName       = name;
                _currentActorId         = id;
                _currentActorRedrawType = s;
                var (actor, _)          = FindCurrentActor();
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
            var (actor, idx) = FindCurrentActor();
            if( actor == null )
            {
                _currentFrame = 0;
                return;
            }

            switch( _currentActorRedrawType )
            {
                case RedrawType.Unload:
                    WriteInvisible( actor, idx );
                    _currentFrame = 0;
                    break;
                case RedrawType.RedrawWithSettings:
                    ChangeSettings();
                    ++_currentFrame;
                    break;
                case RedrawType.RedrawWithoutSettings:
                    WriteVisible( actor, idx );
                    _currentFrame = 0;
                    break;
                case RedrawType.WithoutSettings:
                    WriteInvisible( actor, idx );
                    ++_currentFrame;
                    break;
                case RedrawType.WithSettings:
                    ChangeSettings();
                    WriteInvisible( actor, idx );
                    ++_currentFrame;
                    break;
                case RedrawType.OnlyWithSettings:
                    ChangeSettings();
                    if( !_changedSettings )
                    {
                        return;
                    }

                    WriteInvisible( actor, idx );
                    ++_currentFrame;
                    break;
                default: throw new InvalidEnumArgumentException();
            }
        }

        private void StartRedrawAndWait()
        {
            var (actor, idx) = FindCurrentActor();
            if( actor == null )
            {
                RevertSettings();
                return;
            }

            WriteVisible( actor, idx );
            _currentFrame = _changedSettings ? _currentFrame + 1 : 0;
        }

        private void RevertSettings()
        {
            var (actor, _) = FindCurrentActor();
            if( actor != null )
            {
                if( !StillLoading( RenderPtr( actor ) ) )
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
            if( _pi.ClientState.Condition[ ConditionFlag.BetweenAreas51 ] || _pi.ClientState.Condition[ ConditionFlag.BetweenAreas ] )
            {
                return;
            }

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

        private void RedrawActorIntern( int actorId, string actorName, RedrawType settings )
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

        public void RedrawActor( Actor? actor, RedrawType settings = RedrawType.WithSettings )
        {
            if( actor != null )
            {
                RedrawActorIntern( actor.ActorId, actor.Name, settings );
            }
        }

        private Actor? GetLocalPlayer()
        {
            var gPoseActor = _pi.ClientState.Actors[ GPosePlayerActorIdx ];
            return gPoseActor ?? _pi.ClientState.Actors[ 0 ];
        }

        private Actor? GetName( string name )
        {
            var lowerName = name.ToLowerInvariant();
            return lowerName switch
            {
                ""          => null,
                "<me>"      => GetLocalPlayer(),
                "self"      => GetLocalPlayer(),
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

        public void RedrawActor( string name, RedrawType settings = RedrawType.WithSettings )
            => RedrawActor( GetName( name ), settings );

        public void RedrawAll( RedrawType settings = RedrawType.WithSettings )
        {
            Clear();
            foreach( var actor in _pi.ClientState.Actors )
            {
                RedrawActor( actor, settings );
            }
        }

        private void UnloadAll()
        {
            Clear();
            foreach( var (actor, index) in _pi.ClientState.Actors.Select( ( a, i ) => ( a, i ) ) )
            {
                WriteInvisible( actor, index );
            }
        }

        private void RedrawAllWithoutSettings()
        {
            Clear();
            foreach( var (actor, index) in _pi.ClientState.Actors.Select( ( a, i ) => ( a, i ) ) )
            {
                WriteVisible( actor, index );
            }
        }

        public async void UnloadAtOnceRedrawWithSettings()
        {
            Clear();
            UnloadAll();
            await Task.Delay( UnloadAllRedrawDelay );
            RedrawAll( RedrawType.RedrawWithSettings );
        }

        public async void UnloadAtOnceRedrawWithoutSettings()
        {
            Clear();
            UnloadAll();
            await Task.Delay( UnloadAllRedrawDelay );
            RedrawAllWithoutSettings();
        }

        public void Clear()
        {
            RestoreSettings();
            _currentFrame = 0;
        }

        public void Dispose()
        {
            RevertSettings();
            _actorIds.Clear();
            _pi.Framework.OnUpdateEvent -= OnUpdateEvent;
        }
    }
}