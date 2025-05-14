using OtterGui.Classes;
using Penumbra.Collections;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;

namespace Penumbra.Communication;

/// <summary>
/// Triggered whenever a model recomputes its attribute masks.
/// <list type="number">
///     <item>Parameter is the game object that recomputed its attributes. </item>
///     <item>Parameter is the draw object on which the recomputation was called. </item>
///     <item>Parameter is the collection associated with the game object. </item>
///     <item>Parameter is the slot that was recomputed. If this is Unknown, it is a general new update call. </item>
/// </list> </summary>
public sealed class ModelAttributeComputed()
    : EventWrapper<Actor, Model, ModCollection, HumanSlot, ModelAttributeComputed.Priority>(nameof(ModelAttributeComputed))
{
    public enum Priority
    {
        /// <seealso cref="Meta.ShapeManager.OnAttributeComputed"/>
        ShapeManager = 0,
    }
}
