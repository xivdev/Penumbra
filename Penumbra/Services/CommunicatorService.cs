using OtterGui.Classes;
using OtterGui.Log;
using Penumbra.Communication;

namespace Penumbra.Services;

public class CommunicatorService : IDisposable, Luna.IService
{
    public CommunicatorService(Logger logger)
    {
        EventWrapperBase.ChangeLogger(logger);
    }

    /// <inheritdoc cref="Communication.CollectionChange"/>
    public readonly CollectionChange CollectionChange = new();

    /// <inheritdoc cref="Communication.TemporaryGlobalModChange"/>
    public readonly TemporaryGlobalModChange TemporaryGlobalModChange = new();

    /// <inheritdoc cref="Communication.CreatingCharacterBase"/>
    public readonly CreatingCharacterBase CreatingCharacterBase = new();

    /// <inheritdoc cref="Communication.CreatedCharacterBase"/>
    public readonly CreatedCharacterBase CreatedCharacterBase = new();

    /// <inheritdoc cref="Communication.MtrlLoaded"/>
    public readonly MtrlLoaded MtrlLoaded = new();

    /// <inheritdoc cref="Communication.ModDataChanged"/>
    public readonly ModDataChanged ModDataChanged = new();

    /// <inheritdoc cref="Communication.ModOptionChanged"/>
    public readonly ModOptionChanged ModOptionChanged = new();

    /// <inheritdoc cref="Communication.ModDiscoveryStarted"/>
    public readonly ModDiscoveryStarted ModDiscoveryStarted = new();

    /// <inheritdoc cref="Communication.ModDiscoveryFinished"/>
    public readonly ModDiscoveryFinished ModDiscoveryFinished = new();

    /// <inheritdoc cref="Communication.ModDirectoryChanged"/>
    public readonly ModDirectoryChanged ModDirectoryChanged = new();

    /// <inheritdoc cref="Communication.ModFileChanged"/>
    public readonly ModFileChanged ModFileChanged = new();

    /// <inheritdoc cref="Communication.ModPathChanged"/>
    public readonly ModPathChanged ModPathChanged = new();

    /// <inheritdoc cref="Communication.ModSettingChanged"/>
    public readonly ModSettingChanged ModSettingChanged = new();

    /// <inheritdoc cref="Communication.CollectionInheritanceChanged"/>
    public readonly CollectionInheritanceChanged CollectionInheritanceChanged = new();

    /// <inheritdoc cref="Communication.EnabledChanged"/>
    public readonly EnabledChanged EnabledChanged = new();

    /// <inheritdoc cref="Communication.PreSettingsTabBarDraw"/>
    public readonly PreSettingsTabBarDraw PreSettingsTabBarDraw = new();

    /// <inheritdoc cref="Communication.PreSettingsPanelDraw"/>
    public readonly PreSettingsPanelDraw PreSettingsPanelDraw = new();

    /// <inheritdoc cref="Communication.PostEnabledDraw"/>
    public readonly PostEnabledDraw PostEnabledDraw = new();

    /// <inheritdoc cref="Communication.PostSettingsPanelDraw"/>
    public readonly PostSettingsPanelDraw PostSettingsPanelDraw = new();

    /// <inheritdoc cref="Communication.ChangedItemHover"/>
    public readonly ChangedItemHover ChangedItemHover = new();

    /// <inheritdoc cref="Communication.ChangedItemClick"/>
    public readonly ChangedItemClick ChangedItemClick = new();

    /// <inheritdoc cref="Communication.SelectTab"/>
    public readonly SelectTab SelectTab = new();

    /// <inheritdoc cref="Communication.ResolvedFileChanged"/>
    public readonly ResolvedFileChanged ResolvedFileChanged = new();

    /// <inheritdoc cref="Communication.PcpCreation"/>
    public readonly PcpCreation PcpCreation = new();

    /// <inheritdoc cref="Communication.PcpParsing"/>
    public readonly PcpParsing PcpParsing = new();

    public void Dispose()
    {
        CollectionChange.Dispose();
        TemporaryGlobalModChange.Dispose();
        CreatingCharacterBase.Dispose();
        CreatedCharacterBase.Dispose();
        MtrlLoaded.Dispose();
        ModDataChanged.Dispose();
        ModOptionChanged.Dispose();
        ModDiscoveryStarted.Dispose();
        ModDiscoveryFinished.Dispose();
        ModDirectoryChanged.Dispose();
        ModPathChanged.Dispose();
        ModSettingChanged.Dispose();
        CollectionInheritanceChanged.Dispose();
        EnabledChanged.Dispose();
        PreSettingsTabBarDraw.Dispose();
        PreSettingsPanelDraw.Dispose();
        PostEnabledDraw.Dispose();
        PostSettingsPanelDraw.Dispose();
        ChangedItemHover.Dispose();
        ChangedItemClick.Dispose();
        SelectTab.Dispose();
        ResolvedFileChanged.Dispose();
        PcpCreation.Dispose();
        PcpParsing.Dispose();
    }
}
