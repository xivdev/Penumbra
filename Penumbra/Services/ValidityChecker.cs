using Dalamud.Plugin;
using Luna;

namespace Penumbra.Services;

[Flags]
public enum ValidationError
{
    None                    = 0x00,
    DevPluginExists         = 0x01,
    InvalidSourceRepository = 0x02,
    InvalidInstall = 0x04,
}

public sealed class ValidationService(PenumbraLoader parent) : ISingletonService
{
    public const string Repository = "https://raw.githubusercontent.com/xivdev/Penumbra/master/repo.json";
    public const string SeaOfStars = "https://raw.githubusercontent.com/Ottermandias/SeaOfStars/main/repo.json";

    public const string RepositoryLower = "https://raw.githubusercontent.com/xivdev/penumbra/master/repo.json";
    public const string SeaOfStarsLower = "https://raw.githubusercontent.com/ottermandias/seaofstars/main/repo.json";

    public ValidationError Error { get; private set; }

    public bool Validate()
    {
        if (!CheckDevPluginPenumbra(parent.PluginInterface))
            Error |= ValidationError.DevPluginExists;

        if (!CheckSourceRepo(parent))
            Error |= ValidationError.InvalidSourceRepository;

        if (!CheckInstallValidity(parent))
            Error |= ValidationError.InvalidInstall;

        return Error is ValidationError.None;
    }

    // Because remnants of penumbra in devPlugins cause issues, we check for them to warn users to remove them.
    private static bool CheckDevPluginPenumbra(IDalamudPluginInterface pi)
    {
        var path = Path.Combine(pi.DalamudAssetDirectory.Parent?.FullName ?? "INVALIDPATH", "devPlugins", "Penumbra");
        var dir  = new DirectoryInfo(path);

        try
        {
            return !dir.Exists || !dir.EnumerateFiles("*.dll", SearchOption.AllDirectories).Any();
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not check for dev plugin Penumbra:\n{e}");
            return false;
        }
    }

    // Check if the loaded version of Penumbra itself is in devPlugins.
    private static bool CheckInstallValidity(PenumbraLoader parent)
    {
        if (parent.Source is PluginLoadSource.Dev)
            return true;

        var checkedDirectory = parent.PluginInterface.AssemblyLocation.Directory?.Parent?.Parent?.Name;
        return checkedDirectory?.Equals("installedPlugins", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    // Check if the loaded version of Penumbra is installed from a valid source repo.
    private static bool CheckSourceRepo(PenumbraLoader parent)
    {
        if (parent.Source is PluginLoadSource.Dev)
            return true;

        return parent.PluginInterface.SourceRepository.Trim().ToLowerInvariant() switch
        {
            null            => false,
            RepositoryLower => true,
            SeaOfStarsLower => true,
            _               => false,
        };
    }
}
