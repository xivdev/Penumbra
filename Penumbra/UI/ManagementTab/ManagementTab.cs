using Luna;
using Penumbra.Api.Enums;
using Penumbra.Communication;

namespace Penumbra.UI.ManagementTab;

public enum ManagementTabType
{
    UnusedMods,
    DuplicateMods,
    ForbiddenFiles,
    Cleanup,
    UnusedFiles,
    RedundantFiles,
    TextureOptimization,
}

public sealed class ManagementTab : TabBar<ManagementTabType>, ITab<TabType>, IDisposable
{
    private readonly UiNavigator _navigator;

    public new ReadOnlySpan<byte> Label
        => base.Label;

    public TabType Identifier
        => TabType.Management;

    public ManagementTab(Logger log,
        EphemeralConfig config,
        UnusedModsTab unusedMods,
        DuplicateModsTab duplicateMods,
        ForbiddenFilesTab forbiddenFiles,
        UnusedFilesTab unusedFiles,
        RedundantFilesTab redundantFiles,
        TextureOptimizationTab textureOptimization,
        CleanupTab cleanup,
        UiNavigator navigator)
        : base("Management", log, unusedMods, duplicateMods, forbiddenFiles, unusedFiles, redundantFiles, textureOptimization, cleanup)
    {
        _navigator = navigator;
        NextTab    = config.SelectedManagementTab;
        TabSelected.Subscribe((in tab) => config.SelectedManagementTab = tab, 0);
        _navigator.ManagementTabBar += OnNavigation;
    }

    private void OnNavigation(ManagementTabType tab)
        => NextTab = tab;

    public void DrawContent()
        => Draw();

    public void Dispose()
        => _navigator.ManagementTabBar -= OnNavigation;
}
