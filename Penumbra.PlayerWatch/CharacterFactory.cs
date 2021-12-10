using System;
using System.Reflection;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;

namespace Penumbra.PlayerWatch
{
    public static class CharacterFactory
    {
        private static ConstructorInfo? _characterConstructor;

        private static void Initialize()
        {
            _characterConstructor ??= typeof( Character ).GetConstructor( BindingFlags.NonPublic | BindingFlags.Instance, null, new[]
            {
                typeof( IntPtr ),
            }, null )!;
        }

        private static Character Character( IntPtr address )
        {
            Initialize();
            return ( Character )_characterConstructor?.Invoke( new object[]
            {
                address,
            } )!;
        }

        public static Character? Convert( GameObject? actor )
        {
            if( actor == null )
            {
                return null;
            }

            return actor switch
            {
                PlayerCharacter p => p,
                BattleChara b     => b,
                _ => actor.ObjectKind switch
                {
                    ObjectKind.BattleNpc => Character( actor.Address ),
                    ObjectKind.Companion => Character( actor.Address ),
                    ObjectKind.Retainer  => Character( actor.Address ),
                    ObjectKind.EventNpc  => Character( actor.Address ),
                    _                    => null,
                },
            };
        }
    }

    public static class GameObjectExtensions
    {
        private const int ModelTypeOffset = 0x01B4;

        public static unsafe int ModelType( this GameObject actor )
            => *( int* )( actor.Address + ModelTypeOffset );

        public static unsafe void SetModelType( this GameObject actor, int value )
            => *( int* )( actor.Address + ModelTypeOffset ) = value;
    }
}