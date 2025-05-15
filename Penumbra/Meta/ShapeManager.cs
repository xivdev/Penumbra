using System.Reflection.Metadata.Ecma335;
using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;
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

    private readonly uint[] _temporaryMasks  = new uint[NumSlots];
    private readonly uint[] _temporaryValues = new uint[NumSlots];

    public void Dispose()
        => _attributeHook.Unsubscribe(OnAttributeComputed);

    private unsafe void OnAttributeComputed(Actor actor, Model model, ModCollection collection)
    {
        ComputeCache(model, collection);
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

    private unsafe void ComputeCache(Model human, ModCollection collection)
    {
        if (!collection.HasCache)
            return;

        for (var i = 0; i < NumSlots; ++i)
        {
            _temporaryMasks[i]  = 0;
            _temporaryValues[i] = 0;
            _temporaryIndices[i].Clear();

            var modelIndex = UsedModels[i];
            var model      = human.AsHuman->Models[modelIndex.ToIndex()];
            if (model is null || model->ModelResourceHandle is null)
                continue;

            ref var shapes = ref model->ModelResourceHandle->Shapes;
            foreach (var (shape, index) in shapes.Where(kvp => ShpIdentifier.ValidateCustomShapeString(kvp.Key.Value)))
            {
                if (ShapeString.TryRead(shape.Value, out var shapeString))
                {
                    _temporaryIndices[i].TryAdd(shapeString, index);
                    _temporaryMasks[i] |= (ushort)(1 << index);
                    if (collection.MetaCache!.Shp.State.Count > 0
                     && collection.MetaCache!.Shp.ShouldBeEnabled(shapeString, modelIndex, human.GetArmorChanged(modelIndex).Set))
                        _temporaryValues[i] |= (ushort)(1 << index);
                }
                else
                {
                    Penumbra.Log.Warning($"Trying to read a shape string that is too long: {shape}.");
                }
            }
        }

        UpdateDefaultMasks();
    }

    private void UpdateDefaultMasks()
    {
        foreach (var (shape, topIndex) in _temporaryIndices[1])
        {
            if (shape[4] is (byte)'w' && shape[5] is (byte)'r' && _temporaryIndices[2].TryGetValue(shape, out var handIndex))
            {
                _temporaryValues[1] |= 1u << topIndex;
                _temporaryValues[2] |= 1u << handIndex;
            }

            if (shape[4] is (byte)'w' && shape[5] is (byte)'a' && _temporaryIndices[3].TryGetValue(shape, out var legIndex))
            {
                _temporaryValues[1] |= 1u << topIndex;
                _temporaryValues[3] |= 1u << legIndex;
            }
        }

        foreach (var (shape, bottomIndex) in _temporaryIndices[3])
        {
            if (shape[4] is (byte)'a' && shape[5] is (byte)'n' && _temporaryIndices[4].TryGetValue(shape, out var footIndex))
            {
                _temporaryValues[3] |= 1u << bottomIndex;
                _temporaryValues[4] |= 1u << footIndex;
            }
        }
    }
}
