using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.ImGuiNotification.EventArgs;
using ImSharp;
using Penumbra.Mods.Manager;

namespace Penumbra.Services;

public class InstallNotification(ModImportManager modImportManager, string filePath) : Luna.IMessage
{
    public NotificationType NotificationType
        => NotificationType.Info;

    public string NotificationMessage
        => "A new mod has been found!";

    public TimeSpan NotificationDuration
        => TimeSpan.MaxValue;

    public string NotificationTitle { get; } = Path.GetFileNameWithoutExtension(filePath);

    public string LogMessage
        => $"A new mod has been found: {Path.GetFileName(filePath)}";

    public SeString ChatMessage
        => SeString.Empty;

    public StringU8 StoredMessage
        => StringU8.Empty;

    public StringU8 StoredTooltip
        => StringU8.Empty;

    public void OnNotificationActions(INotificationDrawArgs args)
    {
        var region     = Im.ContentRegion.Available;
        var buttonSize = new Vector2((region.X - Im.Style.ItemSpacing.X) / 2, 0);
        if (Im.Button("Install"u8, buttonSize))
        {
            modImportManager.AddUnpack(filePath);
            args.Notification.DismissNow();
        }

        ImGui.SameLine();
        if (Im.Button("Ignore"u8, buttonSize))
            args.Notification.DismissNow();
    }
}
