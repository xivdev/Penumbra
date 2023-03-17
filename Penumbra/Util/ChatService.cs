using System.Collections.Generic;
using Dalamud.Game.Gui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Plugin;
using Dalamud.Utility;
using Lumina.Excel.GeneratedSheets;
using OtterGui.Log;

namespace Penumbra.Util;

public class ChatService
{
    private readonly Logger    _log;
    private readonly UiBuilder _uiBuilder;
    private readonly ChatGui   _chat;

    public ChatService(Logger log, DalamudPluginInterface pi, ChatGui chat)
    {
        _log       = log;
        _uiBuilder = pi.UiBuilder;
        _chat      = chat;
    }

    public void LinkItem(Item item)
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
            new TextPayload(item.Name),
            new RawPayload(new byte[] { 0x02, 0x27, 0x07, 0xCF, 0x01, 0x01, 0x01, 0xFF, 0x01, 0x03 }),
            new RawPayload(new byte[] { 0x02, 0x13, 0x02, 0xEC, 0x03 }),
        };
        // @formatter:on

        var payload = new SeString(payloadList);

        _chat.PrintChat(new XivChatEntry
        {
            Message = payload,
        });
    }

    public void NotificationMessage(string content, string? title = null, NotificationType type = NotificationType.None)
    {
        var logLevel = type switch
        {
            NotificationType.None    => Logger.LogLevel.Information,
            NotificationType.Success => Logger.LogLevel.Information,
            NotificationType.Warning => Logger.LogLevel.Warning,
            NotificationType.Error   => Logger.LogLevel.Error,
            NotificationType.Info    => Logger.LogLevel.Information,
            _                        => Logger.LogLevel.Debug,
        };
        _uiBuilder.AddNotification(content, title, type);
        _log.Message(logLevel, title.IsNullOrEmpty() ? content : $"[{title}] {content}");
    }
}

public static class SeStringBuilderExtensions
{
    public const ushort Green  = 504;
    public const ushort Yellow = 31;
    public const ushort Red    = 534;
    public const ushort Blue   = 517;
    public const ushort White  = 1;
    public const ushort Purple = 541;

    public static SeStringBuilder AddText(this SeStringBuilder sb, string text, int color, bool brackets = false)
        => sb.AddUiForeground((ushort)color).AddText(brackets ? $"[{text}]" : text).AddUiForegroundOff();

    public static SeStringBuilder AddGreen(this SeStringBuilder sb, string text, bool brackets = false)
        => AddText(sb, text, Green, brackets);

    public static SeStringBuilder AddYellow(this SeStringBuilder sb, string text, bool brackets = false)
        => AddText(sb, text, Yellow, brackets);

    public static SeStringBuilder AddRed(this SeStringBuilder sb, string text, bool brackets = false)
        => AddText(sb, text, Red, brackets);

    public static SeStringBuilder AddBlue(this SeStringBuilder sb, string text, bool brackets = false)
        => AddText(sb, text, Blue, brackets);

    public static SeStringBuilder AddWhite(this SeStringBuilder sb, string text, bool brackets = false)
        => AddText(sb, text, White, brackets);

    public static SeStringBuilder AddPurple(this SeStringBuilder sb, string text, bool brackets = false)
        => AddText(sb, text, Purple, brackets);

    public static SeStringBuilder AddCommand(this SeStringBuilder sb, string command, string description)
        => sb.AddText("    》 ")
            .AddBlue(command)
            .AddText($" - {description}");

    public static SeStringBuilder AddInitialPurple(this SeStringBuilder sb, string word, bool withComma = true)
        => sb.AddPurple($"[{word[0]}]")
            .AddText(withComma ? $"{word[1..]}, " : word[1..]);
}
