using System;
using System.Collections.Generic;

namespace Penumbra.UI.Custom
{
    public static partial class ImGuiRaii
    {
        public static EndStack DeferredEnd( Action a, bool condition = true )
            => new EndStack().Push( a, condition );

        public class EndStack : IDisposable
        {
            private readonly Stack< Action > _cleanActions = new();

            public EndStack Push( Action a, bool condition = true )
            {
                if( condition )
                {
                    _cleanActions.Push( a );
                }

                return this;
            }


            public EndStack Pop( int num = 1 )
            {
                while( num-- > 0 && _cleanActions.TryPop( out var action ) )
                {
                    action.Invoke();
                }

                return this;
            }

            public void Dispose()
                => Pop( _cleanActions.Count );
        }
    }
}