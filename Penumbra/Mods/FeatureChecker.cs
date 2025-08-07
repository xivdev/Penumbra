using System.Collections.Frozen;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using OtterGui.Text;
using Penumbra.Mods.Manager;
using Penumbra.UI.Classes;
using Notification = OtterGui.Classes.Notification;

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
            Penumbra.Messager.AddMessage(new Notification(
                $"Please update Penumbra to use the mod {modName}{(modDirectory != modName ? $" at {modDirectory}" : string.Empty)}!\n\nLoading failed because it requires the unsupported feature{(missingFeatures.Count > 1 ? $"s\n\n\t[{string.Join("], [", missingFeatures)}]." : $" [{missingFeatures.First()}].")}",
                NotificationType.Warning));
            return FeatureFlags.Invalid;
        }

        return featureFlags;
    }

    public static bool Supported(string features)
        => SupportedFlags.ContainsKey(features);

    public static void DrawFeatureFlagInput(ModDataEditor editor, Mod mod, float width)
    {
        const int numButtons   = 5;
        var       innerSpacing = ImGui.GetStyle().ItemInnerSpacing;
        var       size         = new Vector2((width - (numButtons - 1) * innerSpacing.X) / numButtons, 0);
        var       buttonColor  = ImGui.GetColorU32(ImGuiCol.FrameBg);
        var       textColor    = ImGui.GetColorU32(ImGuiCol.TextDisabled);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, innerSpacing)
            .Push(ImGuiStyleVar.FrameBorderSize, 0);
        using (var color = ImRaii.PushColor(ImGuiCol.Border, ColorId.FolderLine.Value())
                   .Push(ImGuiCol.Button, buttonColor)
                   .Push(ImGuiCol.Text,   textColor))
        {
            foreach (var flag in SupportedFlags.Values)
            {
                if (mod.RequiredFeatures.HasFlag(flag))
                {
                    style.Push(ImGuiStyleVar.FrameBorderSize, ImUtf8.GlobalScale);
                    color.Pop(2);
                    if (ImUtf8.Button($"{flag}", size))
                        editor.ChangeRequiredFeatures(mod, mod.RequiredFeatures & ~flag);
                    color.Push(ImGuiCol.Button, buttonColor)
                        .Push(ImGuiCol.Text, textColor);
                    style.Pop();
                }
                else if (ImUtf8.Button($"{flag}", size))
                {
                    editor.ChangeRequiredFeatures(mod, mod.RequiredFeatures | flag);
                }

                ImGui.SameLine();
            }
        }

        if (ImUtf8.ButtonEx("Compute"u8, "Compute the required features automatically from the used features."u8, size))
            editor.ChangeRequiredFeatures(mod, mod.ComputeRequiredFeatures());

        ImGui.SameLine();
        if (ImUtf8.ButtonEx("Clear"u8, "Clear all required features."u8, size))
            editor.ChangeRequiredFeatures(mod, FeatureFlags.None);

        ImGui.SameLine();
        ImUtf8.Text("Required Features"u8);
    }
}
