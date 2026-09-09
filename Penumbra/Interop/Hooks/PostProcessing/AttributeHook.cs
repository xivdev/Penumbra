using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Luna;
using Penumbra.Collections;
using Penumbra.GameData;
using Penumbra.GameData.Interop;
using Penumbra.Interop.PathResolving;
using Penumbra.Meta;

namespace Penumbra.Interop.Hooks.PostProcessing;

/// <summary>
/// Triggered whenever a model recomputes its attribute masks.
/// <list type="number">
///     <item>Parameter is the game object that recomputed its attributes. </item>
///     <item>Parameter is the draw object on which the recomputation was called. </item>
///     <item>Parameter is the collection associated with the game object. </item>
///     <item>Parameter is the slot that was recomputed. If this is Unknown, it is a general new update call. </item>
/// </list> </summary>
public sealed unsafe class AttributeHook : EventBase<AttributeHook.Arguments, AttributeHook.Priority>, IHookService
{
    public enum Priority
    {
        /// <seealso cref="ShapeAttributeManager.OnAttributeComputed"/>
        ShapeAttributeManager = 0,
    }

    private readonly CollectionResolver _resolver;
    private readonly AdvancedConfig     _config;

    public AttributeHook(LunaLogger log, HookManager hooks, AdvancedConfig config, CollectionResolver resolver)
        : base("Update Model Attributes", log)
    {
        _config                    =  config;
        _resolver                  =  resolver;
        _task                      =  hooks.CreateHook<Delegate>(Name, Sigs.UpdateAttributes, Detour, config.EnableCustomShapes);
        config.CustomShapesChanged += OnCustomShapesChange;
    }

    private readonly Task<Hook<Delegate>?> _task;

    public nint Address
        => _task.Result?.Address ?? nint.Zero;

    public void Enable()
        => _task.Result?.Enable();

    public void Disable()
        => _task.Result?.Disable();

    public Task Awaiter
        => _task;

    public bool Finished
        => _task.IsCompletedSuccessfully;

    private delegate void Delegate(Human* human);

    private void Detour(Human* human)
    {
        _task.Result!.Original(human);
        var resolveData          = _resolver.IdentifyCollection((DrawObject*)human, true);
        var identifiedActor      = resolveData.AssociatedGameObject;
        var identifiedCollection = resolveData.ModCollection;
        Penumbra.Log.Excessive($"[{Name}] Invoked on 0x{(ulong)human:X} (0x{identifiedActor:X}).");
        Invoke(new Arguments(identifiedActor, human, identifiedCollection));
    }

    public readonly record struct Arguments(Actor Character, Model Human, ModCollection Collection);

    protected override void Dispose(bool disposing)
    {
        _config.CustomShapesChanged -= OnCustomShapesChange;
        _task.Result?.Dispose();
    }

    private void OnCustomShapesChange(bool newValue, bool _)
    {
        if (newValue)
            Enable();
        else
            Disable();
    }
}
