using Luna;
using Penumbra.Communication;
using Penumbra.Mods.Manager;

namespace Penumbra.UI.ManagementTab;

public sealed class TextureOptimizationTab(ModManager mods, UiNavigator navigator) : ITab<ManagementTabType>
{
    public ReadOnlySpan<byte> Label
        => "Texture Optimization"u8;

    public void               DrawContent()
    {
    }

    public ManagementTabType Identifier
        => ManagementTabType.TextureOptimization;
}
