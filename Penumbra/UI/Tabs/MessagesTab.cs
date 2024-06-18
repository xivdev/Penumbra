using OtterGui.Services;
using OtterGui.Widgets;
using Penumbra.Services;

namespace Penumbra.UI.Tabs;

public class MessagesTab(MessageService messages) : ITab, IUiService
{
    public ReadOnlySpan<byte> Label
        => "Messages"u8;

    public bool IsVisible
        => messages.Count > 0;

    public void DrawContent()
        => messages.Draw();
}
