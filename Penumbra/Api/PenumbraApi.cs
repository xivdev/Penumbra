using System;
using System.IO;
using System.Reflection;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Plugin;
using Lumina.Data;
using Penumbra.GameData.Util;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.Api
{
    public class PenumbraApi : IDisposable, IPenumbraApi
    {
        public int ApiVersion { get; } = 2;
        private readonly Plugin           _plugin;
        private readonly Lumina.GameData? _lumina;
        public bool Valid { get; private set; } = false;

        public PenumbraApi( Plugin penumbra )
        {
            _plugin = penumbra;
            Valid   = true;
            _lumina = ( Lumina.GameData? )_plugin.PluginInterface.Data.GetType()
               .GetField( "gameData", BindingFlags.Instance | BindingFlags.NonPublic )
              ?.GetValue( _plugin.PluginInterface.Data );
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

        private string ResolvePath( string path, ModManager manager, ModCollection collection )
        {
            if( !_plugin.Configuration.IsEnabled )
            {
                return path;
            }
            var gamePath = new GamePath( path );
            var ret      = collection.Cache?.ResolveSwappedOrReplacementPath( gamePath );
            ret ??= manager.Collections.ForcedCollection.Cache?.ResolveSwappedOrReplacementPath( gamePath );
            ret ??= path;
            return ret;
        }

        public string ResolvePath( string path )
        {
            var modManager = Service< ModManager >.Get();
            return ResolvePath( path, modManager, modManager.Collections.DefaultCollection );
        }

        public string ResolvePath( string path, string characterName )
        {
            var modManager = Service< ModManager >.Get();
            return ResolvePath( path, modManager,
                modManager.Collections.CharacterCollection.TryGetValue( characterName, out var collection )
                    ? collection
                    : ModCollection.Empty );
        }

        private T? GetFileIntern< T >( string resolvedPath ) where T : FileResource
        {
            try
            {
                if( Path.IsPathRooted( resolvedPath ) )
                {
                    return _lumina?.GetFileFromDisk< T >( resolvedPath );
                }

                return _plugin.PluginInterface.Data.GetFile< T >( resolvedPath );
            }
            catch( Exception e)
            {
                PluginLog.Warning( $"Could not load file {resolvedPath}:\n{e}" );
                return null;
            }
        }

        public T? GetFile< T >( string gamePath ) where T : FileResource
            => GetFileIntern< T >( ResolvePath( gamePath ) );

        public T? GetFile< T >( string gamePath, string characterName ) where T : FileResource
            => GetFileIntern< T >( ResolvePath( gamePath, characterName ) );
    }
}