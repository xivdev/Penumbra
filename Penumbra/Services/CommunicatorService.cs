using Luna;
using Penumbra.Communication;

namespace Penumbra.Services;

public class CommunicatorService(ServiceManager services) : IService
{
    /// <inheritdoc cref="Communication.CollectionChange"/>
    public readonly CollectionChange CollectionChange = services.GetService<CollectionChange>();

    /// <inheritdoc cref="Communication.TemporaryGlobalModChange"/>
    public readonly TemporaryGlobalModChange TemporaryGlobalModChange = services.GetService<TemporaryGlobalModChange>();

    /// <inheritdoc cref="Communication.CreatingCharacterBase"/>
    public readonly CreatingCharacterBase CreatingCharacterBase = services.GetService<CreatingCharacterBase>();

    /// <inheritdoc cref="Communication.CreatedCharacterBase"/>
    public readonly CreatedCharacterBase CreatedCharacterBase = services.GetService<CreatedCharacterBase>();

    /// <inheritdoc cref="Communication.MtrlLoaded"/>
    public readonly MtrlLoaded MtrlLoaded = services.GetService<MtrlLoaded>();

    /// <inheritdoc cref="Communication.ModDataChanged"/>
    public readonly ModDataChanged ModDataChanged = services.GetService<ModDataChanged>();

    /// <inheritdoc cref="Communication.ModOptionChanged"/>
    public readonly ModOptionChanged ModOptionChanged = services.GetService<ModOptionChanged>();

    /// <inheritdoc cref="Communication.ModDiscoveryStarted"/>
    public readonly ModDiscoveryStarted ModDiscoveryStarted = services.GetService<ModDiscoveryStarted>();

    /// <inheritdoc cref="Communication.ModDiscoveryFinished"/>
    public readonly ModDiscoveryFinished ModDiscoveryFinished = services.GetService<ModDiscoveryFinished>();

    /// <inheritdoc cref="Communication.ModDirectoryChanged"/>
    public readonly ModDirectoryChanged ModDirectoryChanged = services.GetService<ModDirectoryChanged>();

    /// <inheritdoc cref="Communication.ModFileChanged"/>
    public readonly ModFileChanged ModFileChanged = services.GetService<ModFileChanged>();

    /// <inheritdoc cref="Communication.ModPathChanged"/>
    public readonly ModPathChanged ModPathChanged = services.GetService<ModPathChanged>();

    /// <inheritdoc cref="Communication.ModSettingChanged"/>
    public readonly ModSettingChanged ModSettingChanged = services.GetService<ModSettingChanged>();

    /// <inheritdoc cref="Communication.CollectionInheritanceChanged"/>
    public readonly CollectionInheritanceChanged CollectionInheritanceChanged = services.GetService<CollectionInheritanceChanged>();

    /// <inheritdoc cref="Communication.EnabledChanged"/>
    public readonly EnabledChanged EnabledChanged = services.GetService<EnabledChanged>();

    /// <inheritdoc cref="Communication.PreSettingsTabBarDraw"/>
    public readonly PreSettingsTabBarDraw PreSettingsTabBarDraw = services.GetService<PreSettingsTabBarDraw>();

    /// <inheritdoc cref="Communication.PreSettingsPanelDraw"/>
    public readonly PreSettingsPanelDraw PreSettingsPanelDraw = services.GetService<PreSettingsPanelDraw>();

    /// <inheritdoc cref="Communication.PostEnabledDraw"/>
    public readonly PostEnabledDraw PostEnabledDraw = services.GetService<PostEnabledDraw>();

    /// <inheritdoc cref="Communication.PostSettingsPanelDraw"/>
    public readonly PostSettingsPanelDraw PostSettingsPanelDraw = services.GetService<PostSettingsPanelDraw>();

    /// <inheritdoc cref="Communication.ChangedItemHover"/>
    public readonly ChangedItemHover ChangedItemHover = services.GetService<ChangedItemHover>();

    /// <inheritdoc cref="Communication.ChangedItemClick"/>
    public readonly ChangedItemClick ChangedItemClick = services.GetService<ChangedItemClick>();

    /// <inheritdoc cref="Communication.SelectTab"/>
    public readonly SelectTab SelectTab = services.GetService<SelectTab>();

    /// <inheritdoc cref="Communication.ResolvedFileChanged"/>
    public readonly ResolvedFileChanged ResolvedFileChanged = services.GetService<ResolvedFileChanged>();

    /// <inheritdoc cref="Communication.ResolvedFileChanged"/>
    public readonly ResolvedMetaChanged ResolvedMetaChanged = services.GetService<ResolvedMetaChanged>();

    /// <inheritdoc cref="Communication.PcpCreation"/>
    public readonly PcpCreation PcpCreation = services.GetService<PcpCreation>();

    /// <inheritdoc cref="Communication.PcpParsing"/>
    public readonly PcpParsing PcpParsing = services.GetService<PcpParsing>();

    /// <inheritdoc cref="Communication.CharacterUtilityFinished"/>
    public readonly CharacterUtilityFinished LoadingFinished = services.GetService<CharacterUtilityFinished>();
}
