using Dalamud.Interface.Internal.Notifications;
using OtterGui.Classes;

namespace Penumbra.Import.Models;

public record class IoNotifier
{
    /// <summary> Notification subclass so that we have a distinct type to filter by. </summary>
    private class LegallyDistinctNotification : Notification
    {
        public LegallyDistinctNotification(string content, NotificationType type): base(content, type)
        {}
    }

    private readonly DateTime _startTime = DateTime.UtcNow;
    private          string   _context   = "";

    /// <summary> Create a new notifier with the specified context appended to any other context already present. </summary>
    public IoNotifier WithContext(string context)
        => this with { _context = $"{_context}{context}: "};

    /// <summary> Send a warning with any current context to notification channels. </summary>
    public void Warning(string content)
        => SendNotification(content, NotificationType.Warning);

    /// <summary> Get the current warnings for this notifier. </summary>
    /// <remarks> This does not currently filter to notifications with the current notifier's context - it will return all IO notifications from all notifiers. </remarks>
    public IEnumerable<string> GetWarnings()
        => GetFilteredNotifications(NotificationType.Warning);

    /// <summary> Create an exception with any current context. </summary>
    [StackTraceHidden]
    public Exception Exception(string message)
        => Exception<Exception>(message);

    /// <summary> Create an exception of the provided type with any current context. </summary>
    [StackTraceHidden]
    public TException Exception<TException>(string message)
        where TException : Exception, new()
        => (TException)Activator.CreateInstance(typeof(TException), $"{_context}{message}")!;

    private void SendNotification(string message, NotificationType type)
        => Penumbra.Messager.AddMessage(
            new LegallyDistinctNotification($"{_context}{message}", type),
            true, false, true, false
        );

    private IEnumerable<string> GetFilteredNotifications(NotificationType type)
        => Penumbra.Messager
            .Where(p => p.Key >= _startTime && p.Value is LegallyDistinctNotification && p.Value.NotificationType == type)
            .Select(p => p.Value.PrintMessage);
}
