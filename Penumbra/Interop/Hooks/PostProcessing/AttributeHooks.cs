using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.Communication;
using Penumbra.GameData;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;
using Penumbra.Interop.PathResolving;
using Penumbra.Services;

namespace Penumbra.Interop.Hooks.PostProcessing;

public sealed unsafe class AttributeHooks : IRequiredService, IDisposable
{
    private delegate void SetupAttributes(Human* human, byte* data);
    private delegate void AttributeUpdate(Human* human);

    private readonly Configuration          _config;
    private readonly ModelAttributeComputed _event;
    private readonly CollectionResolver     _resolver;

    private readonly AttributeHook[]             _hooks;
    private readonly Task<Hook<AttributeUpdate>> _updateHook;
    private          ModCollection               _identifiedCollection = ModCollection.Empty;
    private          Actor                       _identifiedActor      = Actor.Null;
    private          bool                        _inUpdate;

    public AttributeHooks(Configuration config, CommunicatorService communication, CollectionResolver resolver, HookManager hooks)
    {
        _config   = config;
        _event    = communication.ModelAttributeComputed;
        _resolver = resolver;
        _hooks =
        [
            new AttributeHook(this, hooks, Sigs.SetupTopModelAttributes,  _config.EnableCustomShapes, HumanSlot.Body),
            new AttributeHook(this, hooks, Sigs.SetupHandModelAttributes, _config.EnableCustomShapes, HumanSlot.Hands),
            new AttributeHook(this, hooks, Sigs.SetupLegModelAttributes,  _config.EnableCustomShapes, HumanSlot.Legs),
            new AttributeHook(this, hooks, Sigs.SetupFootModelAttributes, _config.EnableCustomShapes, HumanSlot.Feet),
        ];
        _updateHook = hooks.CreateHook<AttributeUpdate>("UpdateAttributes", Sigs.UpdateAttributes, UpdateAttributesDetour,
            _config.EnableCustomShapes);
    }

    private class AttributeHook
    {
        private readonly AttributeHooks              _parent;
        public readonly  string                      Name;
        public readonly  Task<Hook<SetupAttributes>> Hook;
        public readonly  uint                        ModelIndex;
        public readonly  HumanSlot                   Slot;

        public AttributeHook(AttributeHooks parent, HookManager hooks, string signature, bool enabled, HumanSlot slot)
        {
            _parent    = parent;
            Name       = $"Setup{slot}Attributes";
            Slot       = slot;
            ModelIndex = slot.ToIndex();
            Hook       = hooks.CreateHook<SetupAttributes>(Name, signature, Detour, enabled);
        }

        private void Detour(Human* human, byte* data)
        {
            Penumbra.Log.Excessive($"[{Name}] Invoked on 0x{(ulong)human:X} (0x{_parent._identifiedActor.Address:X}).");
            Hook.Result.Original(human, data);
            if (_parent is { _inUpdate: true, _identifiedActor.Valid: true })
                _parent._event.Invoke(_parent._identifiedActor, human, _parent._identifiedCollection, Slot);
        }
    }

    private void UpdateAttributesDetour(Human* human)
    {
        var resolveData = _resolver.IdentifyCollection((DrawObject*)human, true);
        _identifiedActor      = resolveData.AssociatedGameObject;
        _identifiedCollection = resolveData.ModCollection;
        _inUpdate             = true;
        Penumbra.Log.Excessive($"[UpdateAttributes] Invoked on 0x{(ulong)human:X} (0x{_identifiedActor.Address:X}).");
        _event.Invoke(_identifiedActor, human, _identifiedCollection, HumanSlot.Unknown);
        _updateHook.Result.Original(human);
        _inUpdate = false;
    }

    public void SetState(bool enabled)
    {
        if (_config.EnableCustomShapes == enabled)
            return;

        _config.EnableCustomShapes = enabled;
        _config.Save();
        if (enabled)
        {
            foreach (var hook in _hooks)
                hook.Hook.Result.Enable();
            _updateHook.Result.Enable();
        }
        else
        {
            foreach (var hook in _hooks)
                hook.Hook.Result.Disable();
            _updateHook.Result.Disable();
        }
    }

    public void Dispose()
    {
        foreach (var hook in _hooks)
            hook.Hook.Result.Dispose();
        _updateHook.Result.Dispose();
    }
}
