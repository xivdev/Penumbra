using Dalamud.Plugin.Services;
using OtterGui.Services;
using Penumbra.Mods.Manager;
using Penumbra.String.Classes;

namespace Penumbra.UI;

public sealed class ChatWarningService(IChatGui chatGui, IClientState clientState, ModManager modManager) : IUiService
{
    private readonly Dictionary<string, (DateTime, string)> _lastFileWarnings = [];
    private          int                                    _lastFileWarningsCleanCounter;

    private const           int      LastFileWarningsCleanCycle = 100;
    private static readonly TimeSpan LastFileWarningsMaxAge     = new(1, 0, 0);

    public void CleanLastFileWarnings(bool force)
    {
        if (!force)
        {
            _lastFileWarningsCleanCounter = (_lastFileWarningsCleanCounter + 1) % LastFileWarningsCleanCycle;
            if (_lastFileWarningsCleanCounter != 0)
                return;
        }

        var expiredDate = DateTime.Now - LastFileWarningsMaxAge;
        var toRemove = new HashSet<string>();
        foreach (var (key, value) in _lastFileWarnings)
        {
            if (value.Item1 <= expiredDate)
                toRemove.Add(key);
        }
        foreach (var key in toRemove)
            _lastFileWarnings.Remove(key);
    }

    public void PrintFileWarning(string fullPath, Utf8GamePath originalGamePath, string messageComplement)
    {
        CleanLastFileWarnings(true);

        // Don't warn twice for the same file within a certain time interval unless the reason changed.
        if (_lastFileWarnings.TryGetValue(fullPath, out var lastWarning) && lastWarning.Item2 == messageComplement)
            return;

        // Don't warn for files managed by other plugins, or files we aren't sure about.
        if (!modManager.TryIdentifyPath(fullPath, out var mod, out _))
            return;

        // Don't warn if there's no local player (as an approximation of no chat), so as not to trigger the cooldown.
        if (clientState.LocalPlayer == null)
            return;

        // The wording is an allusion to tar's "Cowardly refusing to create an empty archive"
        chatGui.PrintError($"Cowardly refusing to load replacement for {originalGamePath.Filename().ToString().ToLowerInvariant()} by {mod.Name}{(messageComplement.Length > 0 ? ": " : ".")}{messageComplement}", "Penumbra");
        _lastFileWarnings[fullPath] = (DateTime.Now, messageComplement);
    }
}
