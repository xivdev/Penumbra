using System.Collections.Frozen;
using ImSharp;
using Penumbra.Files;
using Penumbra.Mods.Manager;
using Penumbra.UI.Classes;

namespace Penumbra.Mods;

public static class FeatureChecker
{
    /// <summary> Manually setup supported features to exclude None and Invalid and not make something supported too early. </summary>
    private static readonly FrozenDictionary<StringU8, FeatureFlags> SupportedFlags = new[]
    {
        FeatureFlags.Atch,
        FeatureFlags.Shp,
        FeatureFlags.Atr,
        FeatureFlags.Layout,
    }.ToFrozenDictionary(f => f.ToNameU8(), f => f);

    public static IReadOnlyCollection<StringU8> SupportedFeatures
        => SupportedFlags.Keys;

    public static readonly FrozenSet<string> SupportedFeaturesU16 = SupportedFeatures.Select(s => s.ToString()).ToFrozenSet();

    public static FeatureFlags ParseFlags(Mod mod, IReadOnlyCollection<StringU8> features)
    {
        var               featureFlags    = FeatureFlags.None;
        HashSet<StringU8> missingFeatures = new(features.Count);
        foreach (var feature in features)
        {
            if (SupportedFlags.TryGetValue(feature, out var featureFlag))
                featureFlags |= featureFlag;
            else
                missingFeatures.Add(feature);
        }

        if (missingFeatures.Count > 0)
            throw new MissingFeatureException(mod, missingFeatures);

        return featureFlags;
    }

    public static bool Supported(StringU8 features)
        => SupportedFlags.ContainsKey(features);

    public static bool Supported(string features)
        => SupportedFeaturesU16.Contains(features);

    public static void DrawFeatureFlagInput(ModDataEditor editor, Mod mod, float width)
    {
        const int numButtons   = 5;
        var       innerSpacing = Im.Style.ItemInnerSpacing;
        var       size         = new Vector2((width - (numButtons - 1) * innerSpacing.X) / numButtons, 0);
        var       buttonColor  = Im.Style[ImGuiColor.FrameBackground];
        var       textColor    = Im.Style[ImGuiColor.TextDisabled];
        using (var style = ImStyleBorder.Frame.Push(ColorId.FolderLine.Value(), 0)
                   .Push(ImGuiColor.Button, buttonColor)
                   .Push(ImGuiColor.Text,   textColor))
        {
            foreach (var flag in SupportedFlags.Values)
            {
                if (mod.RequiredFeatures.HasFlag(flag))
                {
                    style.Push(ImStyleSingle.FrameBorderThickness, Im.Style.GlobalScale);
                    style.PopColor(2);
                    if (Im.Button($"{flag}", size))
                        editor.ChangeRequiredFeatures(mod, mod.RequiredFeatures & ~flag);
                    style.Push(ImGuiColor.Button, buttonColor)
                        .Push(ImGuiColor.Text, textColor);
                    style.PopStyle();
                }
                else if (Im.Button($"{flag}", size))
                {
                    editor.ChangeRequiredFeatures(mod, mod.RequiredFeatures | flag);
                }

                Im.Line.SameInner();
            }
        }

        if (ImEx.Button("Compute"u8, size,
                "Compute the required features automatically from the used features.\n\nRight-click to clear all features."u8))
            editor.ChangeRequiredFeatures(mod, mod.ComputeRequiredFeatures());
        if (Im.Item.RightClicked())
            editor.ChangeRequiredFeatures(mod, FeatureFlags.None);

        Im.Line.SameInner();
        Im.Text("Required Features"u8);
    }
}
