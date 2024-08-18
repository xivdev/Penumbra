using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using OtterGui.Classes;
using OtterGui.Services;

namespace Penumbra.Services;

public class ValidityChecker : IService
{
    public const string Repository      = "https://raw.githubusercontent.com/xivdev/Penumbra/master/repo.json";
    public const string SeaOfStars      = "https://raw.githubusercontent.com/Ottermandias/SeaOfStars/main/repo.json";
    public const string RepositoryLower = "https://raw.githubusercontent.com/xivdev/penumbra/master/repo.json";
    public const string SeaOfStarsLower = "https://raw.githubusercontent.com/ottermandias/seaofstars/main/repo.json";

    public readonly bool DevPenumbraExists;
    public readonly bool IsNotInstalledPenumbra;
    public readonly bool IsValidSourceRepo;

    public readonly List<Exception> ImcExceptions = [];

    public readonly string Version;
    public readonly string CommitHash;

    public unsafe string GameVersion
    {
        get
        {
            var framework = Framework.Instance();
            return framework == null ? string.Empty : framework->GameVersionString;
        }
    }

    public ValidityChecker(IDalamudPluginInterface pi)
    {
        DevPenumbraExists      = CheckDevPluginPenumbra(pi);
        IsNotInstalledPenumbra = CheckIsNotInstalled(pi);
        IsValidSourceRepo      = CheckSourceRepo(pi);

        var assembly = GetType().Assembly;
        Version    = assembly.GetName().Version?.ToString() ?? string.Empty;
        CommitHash = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "Unknown";
    }

    public void LogExceptions()
    {
        if (ImcExceptions.Count > 0)
            Penumbra.Messager.NotificationMessage($"{ImcExceptions.Count} IMC Exceptions thrown during Penumbra load. Please repair your game files.",
                NotificationType.Warning);
    }

    // Because remnants of penumbra in devPlugins cause issues, we check for them to warn users to remove them.
    private static bool CheckDevPluginPenumbra(IDalamudPluginInterface pi)
    {
#if !DEBUG
        var path = Path.Combine(pi.DalamudAssetDirectory.Parent?.FullName ?? "INVALIDPATH", "devPlugins", "Penumbra");
        var dir  = new DirectoryInfo(path);

        try
        {
            return dir.Exists && dir.EnumerateFiles("*.dll", SearchOption.AllDirectories).Any();
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not check for dev plugin Penumbra:\n{e}");
            return true;
        }
#else
        return false;
#endif
    }

    // Check if the loaded version of Penumbra itself is in devPlugins.
    private static bool CheckIsNotInstalled(IDalamudPluginInterface pi)
    {
#if !DEBUG
        var checkedDirectory = pi.AssemblyLocation.Directory?.Parent?.Parent?.Name;
        var ret              = checkedDirectory?.Equals("installedPlugins", StringComparison.OrdinalIgnoreCase) ?? false;
        if (!ret)
            Penumbra.Log.Error($"Penumbra is not correctly installed. Application loaded from \"{pi.AssemblyLocation.Directory!.FullName}\".");

        return !ret;
#else
        return false;
#endif
    }

    // Check if the loaded version of Penumbra is installed from a valid source repo.
    private static bool CheckSourceRepo(IDalamudPluginInterface pi)
    {
#if !DEBUG
        return pi.SourceRepository?.Trim().ToLowerInvariant() switch
        {
            null            => false,
            RepositoryLower => true,
            SeaOfStarsLower => true,
            _               => false,
        };
#else
        return true;
#endif
    }
}
