using System;
using System.Linq;
using Dalamud.Game.ClientState.Actors.Types;
using ImGuiNET;

namespace Penumbra.Api
{
    public class PenumbraApi : IDisposable, IPenumbraApi
    {
        public int ApiVersion { get; } = 1;
        private          bool   _initialized = false;
        private readonly Plugin _plugin;

        public PenumbraApi( Plugin penumbra )
        {
            _plugin = penumbra;
            //_plugin.SettingsInterface.ChangedItemClicked += TriggerChangedItemClicked;
            _initialized = true;
        }

        public void Dispose()
        {
            //_plugin.SettingsInterface.ChangedItemClicked -= TriggerChangedItemClicked;
            _initialized = false;
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
            if( !_initialized )
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

        public bool Valid
            => _initialized;
    }
}