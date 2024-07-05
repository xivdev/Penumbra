namespace Penumbra.Interop.Hooks;

public static class HookSettings
{
    public const bool AllHooks = true;

    public const bool ObjectHooks            = true && AllHooks;
    public const bool ReplacementHooks       = true && AllHooks;
    public const bool ResourceHooks          = true && AllHooks;
    public const bool MetaEntryHooks         = true && AllHooks;
    public const bool MetaParentHooks        = true && AllHooks;
    public const bool VfxIdentificationHooks = false && AllHooks;
    public const bool PostProcessingHooks    = true && AllHooks;
}
