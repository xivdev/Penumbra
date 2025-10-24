using System.Collections.Frozen;
using ImSharp;
using Penumbra.Mods.Manager;
using Penumbra.UI.Classes;
using Notification = Luna.Notification;

namespace Penumbra.Mods;

public static class FeatureChecker
{
    /// <summary> Manually setup supported features to exclude None and Invalid and not make something supported too early. </summary>
    private static readonly FrozenDictionary<string, FeatureFlags> SupportedFlags = new[]
    {
        FeatureFlags.Atch,
        FeatureFlags.Shp,
        FeatureFlags.Atr,
    }.ToFrozenDictionary(f => f.ToString(), f => f);

    public static IReadOnlyCollection<string> SupportedFeatures
        => SupportedFlags.Keys;

    public static FeatureFlags ParseFlags(string modDirectory, string modName, IEnumerable<string> features)
    {
        var             featureFlags    = FeatureFlags.None;
        HashSet<string> missingFeatures = [];
        foreach (var feature in features)
        {
            if (SupportedFlags.TryGetValue(feature, out var featureFlag))
                featureFlags |= featureFlag;
            else
                missingFeatures.Add(feature);
        }

        if (missingFeatures.Count > 0)
        {
            Penumbra.Messager.AddMessage(new Notification($"Please update Penumbra to use the mod {modName}{(modDirectory != modName ? $" at {modDirectory}" : string.Empty)}!\n\n"
              + $"Loading failed because it requires the unsupported feature{(missingFeatures.Count > 1 ? $"s\n\n\t[{string.Join("], [", missingFeatures)}]." : $" [{missingFeatures.First()}].")}"));
            return FeatureFlags.Invalid;
        }

        return featureFlags;
    }

    public static bool Supported(string features)
        => SupportedFlags.ContainsKey(features);

    public static void DrawFeatureFlagInput(ModDataEditor editor, Mod mod, float width)
    {
        const int numButtons   = 5;
        var       innerSpacing = Im.Style.ItemInnerSpacing;
        var       size         = new Vector2((width - (numButtons - 1) * innerSpacing.X) / numButtons, 0);
        var       buttonColor  = Im.Style[ImGuiColor.FrameBackground];
        var       textColor    = Im.Style[ImGuiColor.TextDisabled];
        using (var style = ImStyleBorder.Frame.Push(ColorId.FolderLine.Value(), 0)
                   .Push(ImStyleDouble.ItemSpacing, innerSpacing)
                   .Push(ImGuiColor.Button,         buttonColor)
                   .Push(ImGuiColor.Text,           textColor))
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

                Im.Line.Same();
            }
        }

        if (ImEx.Button("Compute"u8, size, "Compute the required features automatically from the used features."u8))
            editor.ChangeRequiredFeatures(mod, mod.ComputeRequiredFeatures());

        Im.Line.Same();
        if (ImEx.Button("Clear"u8, size, "Clear all required features."u8))
            editor.ChangeRequiredFeatures(mod, FeatureFlags.None);

        Im.Line.Same();
        Im.Text("Required Features"u8);
    }
}
