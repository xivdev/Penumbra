using Luna;

namespace Penumbra.Import.Models;

public record IoNotifier(Logger Log)
{
    private readonly List<string> _messages = [];
    private          string       _context  = "";

    /// <summary> Create a new notifier with the specified context appended to any other context already present. </summary>
    public IoNotifier WithContext(string context)
        => this with { _context = $"{_context}{context}: " };

    /// <summary> Send a warning with any current context to notification channels. </summary>
    public void Warning(string content)
        => SendMessage(content, Logger.LogLevel.Warning);

    /// <summary> Get the current warnings for this notifier. </summary>
    /// <remarks> This does not currently filter to notifications with the current notifier's context - it will return all IO notifications from all notifiers. </remarks>
    public IEnumerable<string> GetWarnings()
        => _messages;

    /// <summary> Create an exception with any current context. </summary>
    [StackTraceHidden]
    public Exception Exception(string message)
        => Exception<Exception>(message);

    /// <summary> Create an exception of the provided type with any current context. </summary>
    [StackTraceHidden]
    public TException Exception<TException>(string message)
        where TException : Exception, new()
        => (TException)Activator.CreateInstance(typeof(TException), $"{_context}{message}")!;

    private void SendMessage(string message, Logger.LogLevel type)
    {
        var fullText = $"{_context}{message}";
        Log.Message(type, fullText);
        _messages.Add(fullText);
    }
}
