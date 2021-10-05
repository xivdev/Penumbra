using System;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Penumbra.GameData.Enums;

namespace Penumbra.Api
{
    public class PenumbraIpc : IDisposable
    {
        public const string LabelProviderApiVersion       = "Penumbra.ApiVersion";
        public const string LabelProviderRedrawName       = "Penumbra.RedrawObjectByName";
        public const string LabelProviderRedrawObject     = "Penumbra.RedrawObject";
        public const string LabelProviderRedrawAll        = "Penumbra.RedrawAll";
        public const string LabelProviderResolveDefault   = "Penumbra.ResolveDefaultPath";
        public const string LabelProviderResolveCharacter = "Penumbra.ResolveCharacterPath";

        public const string LabelProviderChangedItemTooltip = "Penumbra.ChangedItemTooltip";
        public const string LabelProviderChangedItemClick   = "Penumbra.ChangedItemClick";

        internal ICallGateProvider< int >?                                        ProviderApiVersion;
        internal ICallGateProvider< string, int, object >?                        ProviderRedrawName;
        internal ICallGateProvider< GameObject, int, object >?                    ProviderRedrawObject;
        internal ICallGateProvider< int, object >?                                ProviderRedrawAll;
        internal ICallGateProvider< string, string >?                             ProviderResolveDefault;
        internal ICallGateProvider< string, string, string >?                     ProviderResolveCharacter;
        internal ICallGateProvider< ChangedItemType, uint, object >?              ProviderChangedItemTooltip;
        internal ICallGateProvider< MouseButton, ChangedItemType, uint, object >? ProviderChangedItemClick;

        internal readonly IPenumbraApi Api;

        private static RedrawType CheckRedrawType( int value )
        {
            var type = ( RedrawType )value;
            if( Enum.IsDefined( type ) )
            {
                return type;
            }

            throw new Exception( "The integer provided for a Redraw Function was not a valid RedrawType." );
        }

        private void OnClick( MouseButton click, object? item )
        {
            var (type, id) = ChangedItemExtensions.ChangedItemToTypeAndId( item );
            ProviderChangedItemClick?.SendMessage( click, type, id );
        }

        private void OnTooltip( object? item )
        {
            var (type, id) = ChangedItemExtensions.ChangedItemToTypeAndId( item );
            ProviderChangedItemTooltip?.SendMessage( type, id );
        }


        public PenumbraIpc( DalamudPluginInterface pi, IPenumbraApi api )
        {
            Api = api;

            try
            {
                ProviderApiVersion = pi.GetIpcProvider< int >( LabelProviderApiVersion );
                ProviderApiVersion.RegisterFunc( () => api.ApiVersion );
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Error registering IPC provider for {LabelProviderApiVersion}:\n{e}" );
            }

            try
            {
                ProviderRedrawName = pi.GetIpcProvider< string, int, object >( LabelProviderRedrawName );
                ProviderRedrawName.RegisterAction( ( s, i ) => api.RedrawObject( s, CheckRedrawType( i ) ) );
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Error registering IPC provider for {LabelProviderRedrawName}:\n{e}" );
            }

            try
            {
                ProviderRedrawObject = pi.GetIpcProvider< GameObject, int, object >( LabelProviderRedrawObject );
                ProviderRedrawObject.RegisterAction( ( o, i ) => api.RedrawObject( o, CheckRedrawType( i ) ) );
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Error registering IPC provider for {LabelProviderRedrawObject}:\n{e}" );
            }

            try
            {
                ProviderRedrawAll = pi.GetIpcProvider< int, object >( LabelProviderRedrawAll );
                ProviderRedrawAll.RegisterAction( i => api.RedrawAll( CheckRedrawType( i ) ) );
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Error registering IPC provider for {LabelProviderRedrawAll}:\n{e}" );
            }

            try
            {
                ProviderResolveDefault = pi.GetIpcProvider< string, string >( LabelProviderResolveDefault );
                ProviderResolveDefault.RegisterFunc( api.ResolvePath );
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Error registering IPC provider for {LabelProviderResolveDefault}:\n{e}" );
            }

            try
            {
                ProviderResolveCharacter = pi.GetIpcProvider< string, string, string >( LabelProviderResolveCharacter );
                ProviderResolveCharacter.RegisterFunc( api.ResolvePath );
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Error registering IPC provider for {LabelProviderResolveCharacter}:\n{e}" );
            }

            try
            {
                ProviderChangedItemTooltip =  pi.GetIpcProvider< ChangedItemType, uint, object >( LabelProviderChangedItemTooltip );
                api.ChangedItemTooltip     += OnTooltip;
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Error registering IPC provider for {LabelProviderChangedItemTooltip}:\n{e}" );
            }

            try
            {
                ProviderChangedItemClick =  pi.GetIpcProvider< MouseButton, ChangedItemType, uint, object >( LabelProviderChangedItemClick );
                api.ChangedItemClicked   += OnClick;
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Error registering IPC provider for {LabelProviderChangedItemClick}:\n{e}" );
            }
        }

        public void Dispose()
        {
            ProviderApiVersion?.UnregisterFunc();
            ProviderRedrawName?.UnregisterAction();
            ProviderRedrawObject?.UnregisterAction();
            ProviderRedrawAll?.UnregisterAction();
            ProviderResolveDefault?.UnregisterFunc();
            ProviderResolveCharacter?.UnregisterFunc();
            Api.ChangedItemClicked -= OnClick;
            Api.ChangedItemTooltip -= OnTooltip;
        }
    }
}