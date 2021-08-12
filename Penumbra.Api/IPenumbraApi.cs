using Dalamud.Game.ClientState.Actors.Types;
using Lumina.Data;

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
        // Triggered when the user hovers over a listed changed object in a mod tab.
        // Can be used to append tooltips.
        public event ChangedItemHover? ChangedItemTooltip;
        // Triggered when the user clicks a listed changed object in a mod tab.
        public event ChangedItemClick? ChangedItemClicked;

        // Queue redrawing of all actors of the given name with the given RedrawType.
        public void RedrawActor( string name, RedrawType setting );

        // Queue redrawing of the specific actor with the given RedrawType. Should only be used when the actor is sure to be valid.
        public void RedrawActor( Actor actor, RedrawType setting );

        // Queue redrawing of all currently available actors with the given RedrawType.
        public void RedrawAll( RedrawType setting );

        // Resolve a given gamePath via Penumbra using the Default and Forced collections.
        // Returns the given gamePath if penumbra would not manipulate it.
        public string ResolvePath(string gamePath);

        // Resolve a given gamePath via Penumbra using the character collection for the given name (if it exists) and the Forced collections.
        // Returns the given gamePath if penumbra would not manipulate it.
        public string ResolvePath( string gamePath, string characterName );

        // Try to load a given gamePath with the resolved path from Penumbra.
        public T? GetFile< T >( string gamePath ) where T : FileResource;

        // Try to load a given gamePath with the resolved path from Penumbra.
        public T? GetFile<T>( string gamePath, string characterName ) where T : FileResource;
    }
}