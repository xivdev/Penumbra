using OtterGui.Widgets;
using Penumbra.Services;

namespace Penumbra.UI.Tabs;

public class MessagesTab : ITab
{
    public ReadOnlySpan<byte> Label
        => "Messages"u8;

    private readonly MessageService _messages;

    public MessagesTab(MessageService messages)
        => _messages = messages;

    public bool IsVisible
        => _messages.Count > 0;

    public void DrawContent()
        => _messages.Draw();
}
