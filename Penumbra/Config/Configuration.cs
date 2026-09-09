using Luna;

namespace Penumbra;

public class Configuration(
    MainConfig main,
    EditingConfig editing,
    AdvancedConfig advanced,
    BehaviorConfig behavior,
    IoConfig io,
    UiConfig ui,
    EphemeralConfig ephemeral,
    FilterConfig filters) : IService
{
    public readonly MainConfig      Main      = main;
    public readonly EditingConfig   Editing   = editing;
    public readonly AdvancedConfig  Advanced  = advanced;
    public readonly BehaviorConfig  Behavior  = behavior;
    public readonly IoConfig        Io        = io;
    public readonly UiConfig        Ui        = ui;
    public readonly EphemeralConfig Ephemeral = ephemeral;
    public readonly FilterConfig    Filters   = filters;
}
