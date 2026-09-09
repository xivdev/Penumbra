using Luna;
using Penumbra.Files;

namespace Penumbra.UI.ManagementTab;

public sealed class ManagementLog<T>(FilenameService files) : FileLogger<T>(files.ManagementLog, LogLevel.Information), IService
{
    private readonly string _prefix = $"[{typeof(T).Name}] ";
    public override string Prefix
        => _prefix;
}
