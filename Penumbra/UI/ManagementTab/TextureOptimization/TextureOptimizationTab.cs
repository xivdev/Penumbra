using Luna;
using Penumbra.Communication;
using Penumbra.Import.Textures;
using Penumbra.Mods.Manager;

namespace Penumbra.UI.ManagementTab;

public sealed class TextureOptimizationTab(ModManager mods, TextureManager textures, UiNavigator navigator, Configuration config, TextureOptimization optimization, ManagementLog<TextureOptimization> log)
    : ITab<ManagementTabType>
{
    private readonly TextureOptimizationTable _table = new(mods, textures, optimization, navigator, config, log);

    public ReadOnlySpan<byte> Label
        => "Texture Optimization"u8;

    public void DrawContent()
        => _table.Draw();

    public ManagementTabType Identifier
        => ManagementTabType.TextureOptimization;
}
