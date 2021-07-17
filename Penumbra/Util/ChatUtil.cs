using System.Collections.Generic;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;

namespace Penumbra.Util
{
    public static class ChatUtil
    {
        private static DalamudPluginInterface? _pi;

        public static void LinkItem( Item item )
        {
            _pi ??= Service< DalamudPluginInterface >.Get();

            var payloadList = new List< Payload >
            {
                new UIForegroundPayload( _pi.Data, ( ushort )( 0x223 + item.Rarity * 2 ) ),
                new UIGlowPayload( _pi.Data, ( ushort )( 0x224       + item.Rarity * 2 ) ),
                new ItemPayload( _pi.Data, item.RowId, false ),
                new UIForegroundPayload( _pi.Data, 500 ),
                new UIGlowPayload( _pi.Data, 501 ),
                new TextPayload( $"{( char )SeIconChar.LinkMarker}" ),
                new UIForegroundPayload( _pi.Data, 0 ),
                new UIGlowPayload( _pi.Data, 0 ),
                new TextPayload( item.Name ),
                new RawPayload( new byte[] { 0x02, 0x27, 0x07, 0xCF, 0x01, 0x01, 0x01, 0xFF, 0x01, 0x03 } ),
                new RawPayload( new byte[] { 0x02, 0x13, 0x02, 0xEC, 0x03 } ),
            };

            var payload = new SeString( payloadList );

            _pi.Framework.Gui.Chat.PrintChat( new XivChatEntry
            {
                MessageBytes = payload.Encode(),
            } );
        }
    }
}