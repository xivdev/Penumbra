using Dalamud.Plugin;
using JetBrains.Annotations;
using Luna;

namespace Penumbra;

[UsedImplicitly]
public sealed class PenumbraLoader(IDalamudPluginInterface pluginInterface)
    : PluginLoader<Penumbra>(pluginInterface, "Penumbra")
{ }
