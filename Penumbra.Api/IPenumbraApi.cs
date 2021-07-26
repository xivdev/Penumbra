using System;
using Dalamud.Game.ClientState.Actors.Types;

namespace Penumbra.Api
{
    public interface IPenumbraApiBase
    {
        public int ApiVersion { get; }
        public bool Valid { get; }
    }

    public enum MouseButton
    {
        None,
        Left,
        Right,
        Middle,
    }

    public delegate void ChangedItemHover( object? item );
    public delegate void ChangedItemClick( MouseButton button, object? item );

    public interface IPenumbraApi : IPenumbraApiBase
    {
        public event ChangedItemHover? ChangedItemTooltip;
        public event ChangedItemClick? ChangedItemClicked;

        public void RedrawActor( string name, RedrawType setting );
        public void RedrawActor( Actor actor, RedrawType setting );
        public void RedrawAll( RedrawType setting );
    }
}