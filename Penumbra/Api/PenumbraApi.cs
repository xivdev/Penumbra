using System;
using Dalamud.Game.ClientState.Actors.Types;

namespace Penumbra.Api
{
    public class PenumbraApi : IDisposable, IPenumbraApi
    {
        public int ApiVersion { get; } = 1;
        private readonly Plugin _plugin;
        public bool Valid { get; private set; } = false;

        public PenumbraApi( Plugin penumbra )
        {
            _plugin      = penumbra;
            Valid = true;
        }

        public void Dispose()
        {
            Valid = false;
        }

        public event ChangedItemClick? ChangedItemClicked;
        public event ChangedItemHover? ChangedItemTooltip;

        internal bool HasTooltip
            => ChangedItemTooltip != null;

        internal void InvokeTooltip( object? it )
            => ChangedItemTooltip?.Invoke( it );

        internal void InvokeClick( MouseButton button, object? it )
            => ChangedItemClicked?.Invoke( button, it );


        private void CheckInitialized()
        {
            if( !Valid )
            {
                throw new Exception( "PluginShare is not initialized." );
            }
        }

        public void RedrawActor( string name, RedrawType setting )
        {
            CheckInitialized();

            _plugin.ActorRefresher.RedrawActor( name, setting );
        }

        public void RedrawActor( Actor? actor, RedrawType setting )
        {
            CheckInitialized();

            _plugin.ActorRefresher.RedrawActor( actor, setting );
        }

        public void RedrawAll( RedrawType setting )
        {
            CheckInitialized();

            _plugin.ActorRefresher.RedrawAll( setting );
        }
    }
}