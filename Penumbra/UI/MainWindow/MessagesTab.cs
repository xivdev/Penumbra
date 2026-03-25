using Luna;
using Penumbra.Api.Enums;
using MessageService = Penumbra.Services.MessageService;

namespace Penumbra.UI.Tabs;

public sealed class MessagesTab(MessageService messages) : ITab<TabType>
{
    public ReadOnlySpan<byte> Label
        => "Messages"u8;

    public bool IsVisible
        => messages.Count > 0;

    public void DrawContent()
        => messages.DrawNotificationLog();

    public TabType Identifier
        => TabType.Messages;
}
