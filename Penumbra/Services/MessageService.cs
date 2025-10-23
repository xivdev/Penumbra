using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using OtterGui.Log;
using OtterGui.Services;
using Penumbra.GameData.Data;
using Penumbra.Mods.Manager;
using Penumbra.String.Classes;
using Notification = OtterGui.Classes.Notification;

namespace Penumbra.Services;

public class MessageService(Logger log, IUiBuilder builder, IChatGui chat, INotificationManager notificationManager)
    : OtterGui.Classes.MessageService(log, builder, chat, notificationManager), IService
{
    public void LinkItem(in Item item)
    {
        // @formatter:off
        var payloadList = new List<Payload>
        {
            new UIForegroundPayload((ushort)(0x223 + item.Rarity * 2)),
            new UIGlowPayload((ushort)(0x224 + item.Rarity * 2)),
            new ItemPayload(item.RowId, false),
            new UIForegroundPayload(500),
            new UIGlowPayload(501),
            new TextPayload($"{(char)SeIconChar.LinkMarker}"),
            new UIForegroundPayload(0),
            new UIGlowPayload(0),
            new TextPayload(item.Name.ExtractTextExtended()),
            new RawPayload([0x02, 0x27, 0x07, 0xCF, 0x01, 0x01, 0x01, 0xFF, 0x01, 0x03]),
            new RawPayload([0x02, 0x13, 0x02, 0xEC, 0x03]),
        };
        // @formatter:on

        var payload = new SeString(payloadList);

        Chat.Print(new XivChatEntry
        {
            Message = payload,
        });
    }

    public void PrintFileWarning(ModManager modManager, string fullPath, Utf8GamePath originalGamePath, string messageComplement)
    {
        // Don't warn for files managed by other plugins, or files we aren't sure about.
        if (!modManager.TryIdentifyPath(fullPath, out var mod, out _))
            return;

        AddTaggedMessage($"{fullPath}.{messageComplement}",
            new Notification(
                $"Cowardly refusing to load replacement for {originalGamePath.Filename().ToString().ToLowerInvariant()} by {mod.Name}{(messageComplement.Length > 0 ? ":\n" : ".")}{messageComplement}",
                NotificationType.Warning, 10000));
    }
}
