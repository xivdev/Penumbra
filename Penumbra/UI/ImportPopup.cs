using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.ImGuiNotification.EventArgs;
using ImSharp;
using Luna;
using Penumbra.Import.Structs;
using Penumbra.Mods.Manager;
using MessageService = Penumbra.Services.MessageService;
using Notification = Luna.Notification;

namespace Penumbra.UI;

/// <summary> Draw the progress information for import. </summary>
public sealed class ImportPopup : Window, INotificationAwareMessage
{
    public const string WindowLabel = "Penumbra Import Status";

    private readonly        ModImportManager _modImportManager;
    private readonly        MessageService   _messageService;
    private readonly        Configuration    _configuration;
    private static readonly Vector2          OneHalf = Vector2.One / 2;

    private IActiveNotification? _notification;
    private string               _notificationTitle      = string.Empty;
    private string               _notificationMessage    = string.Empty;
    private float                _notificationProgress   = 1.0f;
    private bool                 _notificationEnded      = true;
    private bool                 _notificationSuccessful = true;
    private bool                 _openPopup              = false;

    public bool HasNotification
        => _notification is not null;

    public bool WasDrawn      { get; private set; }
    public bool PopupWasDrawn { get; private set; }

    public ImportPopup(ModImportManager modImportManager, MessageService messageService, Configuration configuration)
        : base(WindowLabel,
            WindowFlags.NoCollapse
          | WindowFlags.NoDecoration
          | WindowFlags.NoBackground
          | WindowFlags.NoMove
          | WindowFlags.NoInputs
          | WindowFlags.NoNavFocus
          | WindowFlags.NoFocusOnAppearing
          | WindowFlags.NoBringToFrontOnFocus
          | WindowFlags.NoDocking
          | WindowFlags.NoTitleBar, true)
    {
        _modImportManager   = modImportManager;
        _messageService     = messageService;
        _configuration      = configuration;
        DisableWindowSounds = true;
        IsOpen              = true;
        RespectCloseHotkey  = false;
        Collapsed           = false;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = Vector2.Zero,
            MaximumSize = Vector2.Zero,
        };
    }

    public override void PreOpenCheck()
    {
        WasDrawn      = false;
        PopupWasDrawn = false;
        _modImportManager.TryUnpacking();
        IsOpen = true;

        while (_modImportManager.AddUnpackedMod(out _))
            ;
    }

    public override void Draw()
    {
        WasDrawn = true;
        if (!_modImportManager.IsImporting(out var import))
            return;

        var notificationHadAlreadyEnded = _notificationEnded;
        (_notificationTitle, _notificationMessage, _notificationProgress, _notificationEnded, _notificationSuccessful) =
            import.ComputeNotificationData();

        _notification?.Title           = _notificationTitle;
        _notification?.Type            = NotificationType;
        _notification?.Content         = _notificationMessage;
        _notification?.MinimizedText   = NotificationMinimizedText;
        _notification?.Progress        = _notificationProgress;
        _notification?.UserDismissable = _notificationEnded;
        if (_notificationEnded != notificationHadAlreadyEnded)
            _notification?.InitialDuration = NotificationDuration;

        if (_openPopup)
        {
            Im.Popup.Open("##PenumbraImportPopup"u8);
            _openPopup = false;
        }

        if (!Im.Popup.IsOpen("##PenumbraImportPopup"u8))
        {
            if (_configuration.AlwaysShowDetailedModImport)
            {
                _openPopup = true;
                _notification?.DismissNow();
            }
            else if (_notification is null)
                _messageService.AddMessage(this, false, true, false);
            return;
        }

        var display = Im.Io.DisplaySize;
        var height  = Math.Max(display.Y / 4, 15 * Im.Style.FrameHeightWithSpacing);
        var width   = display.X / 8;
        var size    = new Vector2(width * 2, height);
        Im.Window.SetNextPosition(Im.Viewport.Main.Center, Condition.Always, OneHalf);
        Im.Window.SetNextSize(size);
        using var popup = Im.Popup.Begin("##PenumbraImportPopup"u8, WindowFlags.Modal);
        PopupWasDrawn = true;
        var terminate = false;
        using (var child = Im.Child.Begin("##import"u8, new Vector2(-1, size.Y - Im.Style.FrameHeight * 2)))
        {
            if (child.Success && import.DrawProgressInfo(new Vector2(-1, Im.Style.FrameHeight)))
                if (!Im.Mouse.IsHoveringRectangle(Rectangle.FromSize(Im.Window.Position, Im.Window.Size))
                 && Im.Mouse.IsClicked(MouseButton.Left))
                    terminate = true;
        }

        if (import.State is not ImporterState.Done && !_configuration.AlwaysShowDetailedModImport)
        {
            if (Im.Button("Continue in the Background"u8,
                    new Vector2((Im.ContentRegion.Available.X - Im.GetStyle().ItemSpacing.X) / 2, 0.0f)))
                Im.Popup.CloseCurrent();
            Im.Line.Same();
        }

        terminate |= import.State is ImporterState.Done
            ? Im.Button("Close"u8, Im.ContentRegion.Available with { Y = 0} )
            : import.DrawCancelButton(Im.ContentRegion.Available with { Y = 0 });
        if (terminate)
            _modImportManager.ClearImport();
    }

    #region Luna Message implementation

    private NotificationType NotificationType
        => (_notificationEnded, _notificationSuccessful) switch
        {
            (false, _)    => NotificationType.Info,
            (true, true)  => NotificationType.Success,
            (true, false) => NotificationType.Error,
        };

    private TimeSpan NotificationDuration
        => _notificationEnded && _notificationSuccessful && _configuration.AutoDismissModImportSuccessReports
            ? Notification.DefaultDuration
            : TimeSpan.MaxValue;

    private string NotificationMinimizedText
        => _notificationEnded ? _notificationMessage : _notificationTitle;

    NotificationType IMessage.NotificationType
        => NotificationType;

    string IMessage.NotificationMessage
        => _notificationMessage;

    TimeSpan IMessage.NotificationDuration
        => NotificationDuration;

    string IMessage.NotificationTitle
        => _notificationTitle;

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
        if (_notificationEnded)
        {
            if (Im.Button("Open Report"u8, Im.ContentRegion.Available with { Y = 0 }))
            {
                _openPopup = true;
                _notification?.DismissNow();
            }

            if (Im.Window.Hovered() && Im.Mouse.IsClicked(MouseButton.Middle))
                _notification?.DismissNow();
        }
        else
        {
            if (Im.Button("Show Details"u8, new Vector2((Im.ContentRegion.Available.X - Im.GetStyle().ItemSpacing.X) * 0.5f, 0.0f)))
            {
                _openPopup = true;
                _notification?.DismissNow();
            }

            Im.Line.Same();
            if (_modImportManager.IsImporting(out var import) && import.DrawCancelButton(-Vector2.UnitX))
            {
                _modImportManager.ClearImport();
                _notification?.DismissNow();
            }
        }
    }

    void INotificationAwareMessage.OnNotificationCreated(IActiveNotification notification)
    {
        var previousNotification = _notification;
        _notification = notification;
        previousNotification?.DismissNow();
        notification.MinimizedText   =  NotificationMinimizedText;
        notification.Progress        =  _notificationProgress;
        notification.UserDismissable =  _notificationEnded;
        notification.Dismiss         += OnNotificationDismissed;
    }

    private void OnNotificationDismissed(INotificationDismissArgs args)
    {
        if (args.Notification != _notification)
            return;

        _notification = null;
        if (!_openPopup && !PopupWasDrawn)
            _modImportManager.ClearImport();
    }

    #endregion
}
