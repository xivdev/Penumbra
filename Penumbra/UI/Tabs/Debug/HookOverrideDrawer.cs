using Dalamud.Plugin;
using ImGuiNET;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.Interop.Hooks;

namespace Penumbra.UI.Tabs.Debug;

public class HookOverrideDrawer(IDalamudPluginInterface pluginInterface) : IUiService
{
    private HookOverrides? _overrides;

    public void Draw()
    {
        using var header = ImUtf8.CollapsingHeaderId("Generate Hook Override"u8);
        if (!header)
            return;

        _overrides ??= HookOverrides.Instance.Clone();

        if (ImUtf8.Button("Save"u8))
            _overrides.Write(pluginInterface);

        ImGui.SameLine();
        var path   = Path.Combine(pluginInterface.GetPluginConfigDirectory(), HookOverrides.FileName);
        var exists = File.Exists(path);
        if (ImUtf8.ButtonEx("Delete"u8, disabled: !exists, tooltip: exists ? ""u8 : "File does not exist."u8))
            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                Penumbra.Log.Error($"Could not delete hook override file at {path}:\n{ex}");
            }

        bool? all = null;
        ImGui.SameLine();
        if (ImUtf8.Button("Disable All Hooks"u8))
            all = true;
        ImGui.SameLine();
        if (ImUtf8.Button("Enable All Hooks"u8))
            all = false;

        foreach (var propertyField in typeof(HookOverrides).GetFields().Where(f => f is { IsStatic: false, FieldType.IsValueType: true }))
        {
            using var tree = ImUtf8.TreeNode(propertyField.Name);
            if (!tree)
                continue;

            var property = propertyField.GetValue(_overrides);
            foreach (var valueField in propertyField.FieldType.GetFields())
            {
                var value = valueField.GetValue(property) as bool? ?? false;
                if (ImUtf8.Checkbox($"Disable {valueField.Name}", ref value) || all.HasValue)
                {
                    valueField.SetValue(property, all ?? value);
                    propertyField.SetValue(_overrides, property);
                }
            }
        }
    }
}
