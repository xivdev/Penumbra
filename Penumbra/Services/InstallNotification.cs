using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.ImGuiNotification.EventArgs;
using OtterGui.Text;
using Penumbra.Mods.Manager;

namespace Penumbra.Services;

public class InstallNotification(ModImportManager modImportManager, string filePath) : OtterGui.Classes.MessageService.IMessage
{
    public string Message
        => "A new mod has been found!";

    public NotificationType NotificationType
        => NotificationType.Info;

    public uint NotificationDuration
        => uint.MaxValue;

    public string NotificationTitle { get; } = Path.GetFileNameWithoutExtension(filePath);

    public string LogMessage
        => $"A new mod has been found: {Path.GetFileName(filePath)}";

    public void OnNotificationActions(INotificationDrawArgs args)
    {
        var region     = ImGui.GetContentRegionAvail();
        var buttonSize = new Vector2((region.X - ImGui.GetStyle().ItemSpacing.X) / 2, 0);
        if (ImUtf8.ButtonEx("Install"u8, ""u8, buttonSize))
        {
            modImportManager.AddUnpack(filePath);
            args.Notification.DismissNow();
        }

        ImGui.SameLine();
        if (ImUtf8.ButtonEx("Ignore"u8, ""u8, buttonSize))
            args.Notification.DismissNow();
    }
}
