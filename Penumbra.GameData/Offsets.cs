namespace Penumbra.GameData;

public static class Offsets
{
    // ActorManager.Data
    public const int AgentCharaCardDataWorldId = 0xC0;

    // ObjectIdentification
    public const  int DrawObjectGetModelTypeVfunc = 50;
    private const int DrawObjectModelBase         = 0x8E8;
    public const  int DrawObjectModelUnk1         = DrawObjectModelBase;
    public const  int DrawObjectModelUnk2         = DrawObjectModelBase + 0x08;
    public const  int DrawObjectModelUnk3         = DrawObjectModelBase + 0x20;
    public const  int DrawObjectModelUnk4         = DrawObjectModelBase + 0x28;

    // PathResolver.AnimationState
    public const int GetGameObjectIdxVfunc = 28;
    public const int TimeLinePtr           = 0x50;

    // PathResolver.Meta
    public const int UpdateModelSkip     = 0x90c;
    public const int GetEqpIndirectSkip1 = 0xA30;
    public const int GetEqpIndirectSkip2 = 0xA28;

    // FontReloader
    public const int ReloadFontsVfunc = 43;

    // ObjectReloader
    public const int EnableDrawVfunc  = 16;
    public const int DisableDrawVfunc = 17;

    // ResourceHandle
    public const int ResourceHandleGetDataVfunc   = 23;
    public const int ResourceHandleGetLengthVfunc = 17;
}
