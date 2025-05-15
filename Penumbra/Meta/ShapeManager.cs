using System.Reflection.Metadata.Ecma335;
using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.Collections.Cache;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Hooks.PostProcessing;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Meta;

public class ShapeManager : IRequiredService, IDisposable
{
    public const     int           NumSlots      = 14;
    public const     int           ModelSlotSize = 18;
    private readonly AttributeHook _attributeHook;

    public static ReadOnlySpan<HumanSlot> UsedModels
        =>
        [
            HumanSlot.Head, HumanSlot.Body, HumanSlot.Hands, HumanSlot.Legs, HumanSlot.Feet, HumanSlot.Ears, HumanSlot.Neck, HumanSlot.Wrists,
            HumanSlot.RFinger, HumanSlot.LFinger, HumanSlot.Glasses, HumanSlot.Hair, HumanSlot.Face, HumanSlot.Ear,
        ];

    public ShapeManager(AttributeHook attributeHook)
    {
        _attributeHook = attributeHook;
        _attributeHook.Subscribe(OnAttributeComputed, AttributeHook.Priority.ShapeManager);
    }

    private readonly Dictionary<ShapeString, short>[] _temporaryIndices =
        Enumerable.Range(0, NumSlots).Select(_ => new Dictionary<ShapeString, short>()).ToArray();

    private readonly uint[]      _temporaryMasks  = new uint[NumSlots];
    private readonly uint[]      _temporaryValues = new uint[NumSlots];
    private readonly PrimaryId[] _ids             = new PrimaryId[ModelSlotSize];

    public void Dispose()
        => _attributeHook.Unsubscribe(OnAttributeComputed);

    private unsafe void OnAttributeComputed(Actor actor, Model model, ModCollection collection)
    {
        if (!collection.HasCache)
            return;

        ComputeCache(model, collection.MetaCache!.Shp);
        for (var i = 0; i < NumSlots; ++i)
        {
            if (_temporaryMasks[i] is 0)
                continue;

            var modelIndex  = UsedModels[i];
            var currentMask = model.AsHuman->Models[modelIndex.ToIndex()]->EnabledShapeKeyIndexMask;
            var newMask     = (currentMask & ~_temporaryMasks[i]) | _temporaryValues[i];
            Penumbra.Log.Excessive($"Changed Model Mask from {currentMask:X} to {newMask:X}.");
            model.AsHuman->Models[modelIndex.ToIndex()]->EnabledShapeKeyIndexMask = newMask;
        }
    }

    private unsafe void ComputeCache(Model human, ShpCache cache)
    {
        for (var i = 0; i < NumSlots; ++i)
        {
            _temporaryMasks[i]  = 0;
            _temporaryValues[i] = 0;
            _temporaryIndices[i].Clear();

            var modelIndex = UsedModels[i];
            var model      = human.AsHuman->Models[modelIndex.ToIndex()];
            if (model is null || model->ModelResourceHandle is null)
                continue;

            _ids[(int)modelIndex] = human.GetArmorChanged(modelIndex).Set;

            ref var shapes = ref model->ModelResourceHandle->Shapes;
            foreach (var (shape, index) in shapes.Where(kvp => ShpIdentifier.ValidateCustomShapeString(kvp.Key.Value)))
            {
                if (ShapeString.TryRead(shape.Value, out var shapeString))
                {
                    _temporaryIndices[i].TryAdd(shapeString, index);
                    _temporaryMasks[i] |= (ushort)(1 << index);
                    if (cache.State.Count > 0
                     && cache.ShouldBeEnabled(shapeString, modelIndex, _ids[(int)modelIndex]))
                        _temporaryValues[i] |= (ushort)(1 << index);
                }
                else
                {
                    Penumbra.Log.Warning($"Trying to read a shape string that is too long: {shape}.");
                }
            }
        }

        UpdateDefaultMasks(cache);
    }

    private void UpdateDefaultMasks(ShpCache cache)
    {
        foreach (var (shape, topIndex) in _temporaryIndices[1])
        {
            if (shape.IsWrist() && _temporaryIndices[2].TryGetValue(shape, out var handIndex))
            {
                _temporaryValues[1] |= 1u << topIndex;
                _temporaryValues[2] |= 1u << handIndex;
                CheckCondition(shape, HumanSlot.Body, HumanSlot.Hands, 1, 2);
            }

            if (shape.IsWaist() && _temporaryIndices[3].TryGetValue(shape, out var legIndex))
            {
                _temporaryValues[1] |= 1u << topIndex;
                _temporaryValues[3] |= 1u << legIndex;
                CheckCondition(shape, HumanSlot.Body, HumanSlot.Legs, 1, 3);
            }
        }

        foreach (var (shape, bottomIndex) in _temporaryIndices[3])
        {
            if (shape.IsAnkle() && _temporaryIndices[4].TryGetValue(shape, out var footIndex))
            {
                _temporaryValues[3] |= 1u << bottomIndex;
                _temporaryValues[4] |= 1u << footIndex;
                CheckCondition(shape, HumanSlot.Legs, HumanSlot.Feet, 3, 4);
            }
        }

        return;

        void CheckCondition(in ShapeString shape, HumanSlot slot1, HumanSlot slot2, int idx1, int idx2)
        {
            if (!cache.CheckConditionState(shape, out var dict))
                return;

            foreach (var (subShape, set) in dict)
            {
                if (set.Contains(slot1, _ids[idx1]))
                    if (_temporaryIndices[idx1].TryGetValue(subShape, out var subIndex))
                        _temporaryValues[idx1] |= 1u << subIndex;
                if (set.Contains(slot2, _ids[idx2]))
                    if (_temporaryIndices[idx2].TryGetValue(subShape, out var subIndex))
                        _temporaryValues[idx2] |= 1u << subIndex;
            }
        }
    }
}
