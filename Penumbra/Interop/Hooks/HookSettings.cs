namespace Penumbra.Interop.Hooks;

public static class HookSettings
{
    public const bool AllHooks = true;

    public const bool ObjectHooks            = false && AllHooks;
    public const bool ReplacementHooks       = true && AllHooks;
    public const bool ResourceHooks          = false && AllHooks;
    public const bool MetaEntryHooks         = false && AllHooks;
    public const bool MetaParentHooks        = false && AllHooks;
    public const bool VfxIdentificationHooks = false && AllHooks;
    public const bool PostProcessingHooks    = false && AllHooks;
}
