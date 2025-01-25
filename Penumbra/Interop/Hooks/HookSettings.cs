using Dalamud.Plugin;
using Newtonsoft.Json;

namespace Penumbra.Interop.Hooks;

public class HookOverrides
{
    [JsonIgnore]
    public bool IsCustomLoaded { get; private set; }

    public static HookOverrides Instance = new();

    public AnimationHooks       Animation;
    public MetaHooks            Meta;
    public ObjectHooks          Objects;
    public PostProcessingHooks  PostProcessing;
    public ResourceLoadingHooks ResourceLoading;
    public ResourceHooks        Resources;

    public HookOverrides Clone()
        => new()
        {
            Animation       = Animation,
            Meta            = Meta,
            Objects         = Objects,
            PostProcessing  = PostProcessing,
            ResourceLoading = ResourceLoading,
            Resources       = Resources,
        };

    public struct AnimationHooks
    {
        public bool ApricotListenerSoundPlayCaller;
        public bool CharacterBaseLoadAnimation;
        public bool Dismount;
        public bool LoadAreaVfx;
        public bool LoadCharacterSound;
        public bool LoadCharacterVfx;
        public bool LoadTimelineResources;
        public bool PlayFootstep;
        public bool ScheduleClipUpdate;
        public bool SomeActionLoad;
        public bool SomeMountAnimation;
        public bool SomePapLoad;
        public bool SomeParasolAnimation;
        public bool GetCachedScheduleResource;
        public bool LoadActionTmb;
    }

    public struct MetaHooks
    {
        public bool CalculateHeight;
        public bool ChangeCustomize;
        public bool EqdpAccessoryHook;
        public bool EqdpEquipHook;
        public bool EqpHook;
        public bool EstHook;
        public bool GmpHook;
        public bool ModelLoadComplete;
        public bool RspBustHook;
        public bool RspHeightHook;
        public bool RspSetupCharacter;
        public bool RspTailHook;
        public bool SetupVisor;
        public bool UpdateModel;
        public bool UpdateRender;
        public bool AtchCaller1;
        public bool AtchCaller2;
    }

    public struct ObjectHooks
    {
        public bool CharacterBaseDestructor;
        public bool CharacterDestructor;
        public bool CopyCharacter;
        public bool CreateCharacterBase;
        public bool EnableDraw;
        public bool WeaponReload;
        public bool SetupPlayerNpc;
        public bool ConstructCutsceneCharacter;
    }

    public struct PostProcessingHooks
    {
        public bool HumanSetupScaling;
        public bool HumanCreateDeformer;
        public bool HumanOnRenderMaterial;
        public bool ModelRendererOnRenderMaterial;
        public bool ModelRendererUnkFunc;
        public bool PrepareColorTable;
        public bool RenderTargetManagerInitialize;
    }

    public struct ResourceLoadingHooks
    {
        public bool CreateFileWHook;
        public bool PapHooks;
        public bool ReadSqPack;
        public bool IncRef;
        public bool DecRef;
        public bool GetResourceSync;
        public bool GetResourceAsync;
        public bool UpdateResourceState;
        public bool CheckFileState;
        public bool TexResourceHandleOnLoad;
        public bool LoadMdlFileExtern;
        public bool SoundOnLoad;
    }

    public struct ResourceHooks
    {
        public bool ApricotResourceLoad;
        public bool LoadMtrl;
        public bool LoadMtrlTex;
        public bool ResolvePathHooks;
        public bool ResourceHandleDestructor;
    }

    public const string FileName = "HookOverrides.json";

    public static HookOverrides LoadFile(IDalamudPluginInterface pi)
    {
        var path = Path.Combine(pi.GetPluginConfigDirectory(), FileName);
        if (!File.Exists(path))
            return new HookOverrides();

        try
        {
            var text = File.ReadAllText(path);
            var ret  = JsonConvert.DeserializeObject<HookOverrides>(text)!;
            ret.IsCustomLoaded = true;
            Penumbra.Log.Warning("A hook override file was loaded, some hooks may be disabled and Penumbra might not be working as expected.");
            return ret;
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error($"A hook override file was found at {path}, but could not be loaded:\n{ex}");
            return new HookOverrides();
        }
    }

    public void Write(IDalamudPluginInterface pi)
    {
        var path = Path.Combine(pi.GetPluginConfigDirectory(), FileName);
        try
        {
            var text = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(path, text);
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error($"Could not write hook override file to {path}:\n{ex}");
        }
    }
}
