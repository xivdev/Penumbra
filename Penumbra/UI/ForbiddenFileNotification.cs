using System.Collections.Frozen;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.ImGuiNotification.EventArgs;
using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.Mods.Editor;
using Penumbra.Services;
using Penumbra.String.Classes;
using Penumbra.UI.ManagementTab;

namespace Penumbra.UI;

public sealed class ForbiddenFileNotification(
    Services.MessageService service,
    UiNavigator navigator)
    : INotificationAwareMessage, IService
{
    private IActiveNotification? _currentNotification;

    public bool IsRedirectionSupported(Utf8GamePath path, IMod mod, bool temporaryCollection)
    {
        if (ForbiddenFilesTab.ForbiddenFiles.ContainsKey((uint)path.Path.Crc32))
        {
            if (!temporaryCollection)
                AddFile(path, mod);
            return false;
        }

        var ext = path.Extension().AsciiToLower().ToString();
        switch (ext)
        {
            case ".atch" or ".eqp" or ".eqdp" or ".est" or ".gmp" or ".cmp" or ".imc":
                if (!temporaryCollection)
                    Penumbra.Messager.NotificationMessage(
                        $"Redirection of {ext} files for {mod.Name} is unsupported. This probably means that the mod is outdated and may not work correctly.\n\nPlease tell the mod creator to use the corresponding meta manipulations instead.",
                        NotificationType.Warning);
                return false;
            case ".lvb" or ".lgb" or ".sgb":
                if (!temporaryCollection)
                    Penumbra.Messager.NotificationMessage(
                        $"Redirection of {ext} files for {mod.Name} is unsupported as this breaks the game.\n\nThis mod will probably not work correctly.",
                        NotificationType.Warning);
                return false;
            default: return true;
        }
    }

    private void AddFile(Utf8GamePath path, IMod mod)
    {
        var t = (path.ToString(), mod.Name);
        if (_gatheredFiles.Contains(t))
            return;

        _gatheredFiles.Add(t);
        service.AddMessage(new StoredNotification(this, t.Item1, t.Name), true, false, true, false);
        if (_currentNotification is null)
        {
            service.AddMessage(this, false, true, false, false);
        }
        else
        {
            _currentNotification.Title         = ((IMessage)this).NotificationTitle;
            _currentNotification.MinimizedText = _currentNotification.Title;
            _currentNotification.ExtendBy(TimeSpan.FromSeconds(30));
        }
    }

    private readonly List<(string File, string Mod)> _gatheredFiles = [];

    private NotificationType NotificationType
        => NotificationType.Warning;

    private TimeSpan NotificationDuration
        => TimeSpan.FromSeconds(30);

    NotificationType IMessage.NotificationType
        => NotificationType;

    string IMessage.NotificationMessage
        => "Redirection of these files is forbidden because unexpected replacements will cause crashes.\n\n"
          + "See Management -> Forbidden Files for more details.";

    TimeSpan IMessage.NotificationDuration
        => NotificationDuration;

    string IMessage.NotificationTitle
        => $"{_gatheredFiles.Count} Forbidden File{(_gatheredFiles.Count is 1 ? string.Empty : "s")} Encountered";

    string IMessage.LogMessage
        => string.Empty;

    SeString IMessage.ChatMessage
        => SeString.Empty;

    StringU8 IMessage.StoredMessage
        => StringU8.Empty;

    StringU8 IMessage.StoredTooltip
        => StringU8.Empty;

    void IMessage.OnNotificationActions(INotificationDrawArgs args)
    {
        var width = Im.ContentRegion.Available with { Y = 0 };
        width.X = (width.X - Im.Style.ItemInnerSpacing.X) / 2;
        if (Im.Button("Open Messages"u8, width))
            navigator.OpenTo(TabType.Messages);
        Im.Line.SameInner();
        if (Im.Button("Open Management"u8, width))
            navigator.OpenTo(ManagementTabType.ForbiddenFiles);
    }

    void INotificationAwareMessage.OnNotificationCreated(IActiveNotification notification)
    {
        _currentNotification       =  notification;
        notification.Dismiss       += OnNotificationDismissed;
        notification.MinimizedText =  _currentNotification.Title;
    }

    private void OnNotificationDismissed(INotificationDismissArgs args)
    {
        if (args.Notification != _currentNotification)
            return;

        _gatheredFiles.Clear();
        _currentNotification = null;
    }

    private sealed class StoredNotification(ForbiddenFileNotification parent, string file, string mod) : IMessage
    {
        public NotificationType NotificationType
            => NotificationType.Warning;

        public string NotificationMessage
            => string.Empty;

        public string NotificationTitle
            => string.Empty;

        public TimeSpan NotificationDuration
            => TimeSpan.Zero;

        public string LogMessage { get; } = $"Redirection of {file} in mod {mod} stopped.";

        public SeString ChatMessage
            => SeString.Empty;

        public StringU8 StoredMessage { get; } = new($"{file} in {mod}: Forbidden File Redirection.");
        public StringU8 StoredTooltip { get; } = new($"File: {file}\nMod: {mod}");

        public void OnNotificationActions(INotificationDrawArgs args)
        { }

        public void OnRemoval()
        {
            parent._gatheredFiles.Remove((file, mod));
            if (parent._currentNotification is {} notification)
            {
                if (parent._gatheredFiles.Count is 0)
                    notification.DismissNow();
                notification.Title         = ((IMessage)parent).NotificationTitle;
                notification.MinimizedText = notification.Title;
            }
        }
    }
}
