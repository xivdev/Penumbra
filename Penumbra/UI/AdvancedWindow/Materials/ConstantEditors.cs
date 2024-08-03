using System.Collections.Frozen;
using OtterGui.Text.Widget.Editors;
using Penumbra.GameData.Files.ShaderStructs;

namespace Penumbra.UI.AdvancedWindow.Materials;

public static class ConstantEditors
{
    public static readonly IEditor<byte> DefaultFloat      = Editors.DefaultFloat.AsByteEditor();
    public static readonly IEditor<byte> DefaultInt        = Editors.DefaultInt.AsByteEditor();
    public static readonly IEditor<byte> DefaultIntAsFloat = Editors.DefaultInt.IntAsFloatEditor().AsByteEditor();
    public static readonly IEditor<byte> DefaultColor      = ColorEditor.HighDynamicRange.Reinterpreting<byte>();

    /// <summary>
    /// Material constants known to be encoded as native <see cref="int"/>s.
    /// 
    /// A <see cref="float"/> editor is nonfunctional for them, as typical values for these constants would fall into the IEEE 754 denormalized number range.
    /// </summary>
    private static readonly FrozenSet<Name> KnownIntConstants;

    static ConstantEditors()
    {
        IReadOnlyList<Name> knownIntConstants = [
            "g_ToonIndex",
            "g_ToonSpecIndex",
        ];

        KnownIntConstants = knownIntConstants.ToFrozenSet();
    }

    public static IEditor<byte> DefaultFor(Name name, MaterialTemplatePickers? materialTemplatePickers = null)
    {
        if (materialTemplatePickers != null)
        {
            if (name == Names.SphereMapIndexConstantName)
                return materialTemplatePickers.SphereMapIndexPicker;
            else if (name == Names.TileIndexConstantName)
                return materialTemplatePickers.TileIndexPicker;
        }

        if (name.Value != null && name.Value.EndsWith("Color"))
            return DefaultColor;

        if (KnownIntConstants.Contains(name))
            return DefaultInt;

        return DefaultFloat;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEditor<byte> AsByteEditor<T>(this IEditor<T> inner) where T : unmanaged
        => inner.Reinterpreting<byte>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEditor<float> IntAsFloatEditor(this IEditor<int> inner)
        => inner.Converting<float>(value => int.CreateSaturating(MathF.Round(value)), value => value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEditor<T> WithExponent<T>(this IEditor<T> inner, T exponent)
        where T : unmanaged, IPowerFunctions<T>, IComparisonOperators<T, T, bool>
        => exponent == T.MultiplicativeIdentity
            ? inner
            : inner.Converting(value => value < T.Zero ? -T.Pow(-value, T.MultiplicativeIdentity / exponent) : T.Pow(value, T.MultiplicativeIdentity / exponent), value => value < T.Zero ? -T.Pow(-value, exponent) : T.Pow(value, exponent));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEditor<T> WithFactorAndBias<T>(this IEditor<T> inner, T factor, T bias)
        where T : unmanaged, IMultiplicativeIdentity<T, T>, IAdditiveIdentity<T, T>, IMultiplyOperators<T, T, T>, IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IDivisionOperators<T, T, T>, IEqualityOperators<T, T, bool>
        => factor == T.MultiplicativeIdentity && bias == T.AdditiveIdentity
            ? inner
            : inner.Converting(value => (value - bias) / factor, value => value * factor + bias);
}
