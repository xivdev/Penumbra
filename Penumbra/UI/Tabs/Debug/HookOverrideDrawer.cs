using Dalamud.Plugin;
using ImSharp;
using Penumbra.Interop.Hooks;

namespace Penumbra.UI.Tabs.Debug;

public class HookOverrideDrawer(IDalamudPluginInterface pluginInterface) : Luna.IUiService
{
    private HookOverrides? _overrides;

    public void Draw()
    {
        using var header = Im.Tree.HeaderId("Generate Hook Override"u8);
        if (!header)
            return;

        _overrides ??= HookOverrides.Instance.Clone();

        if (Im.Button("Save"u8))
            _overrides.Write(pluginInterface);

        Im.Line.Same();
        var path   = Path.Combine(pluginInterface.GetPluginConfigDirectory(), HookOverrides.FileName);
        var exists = File.Exists(path);
        if (ImEx.Button("Delete"u8, disabled: !exists, tooltip: exists ? ""u8 : "File does not exist."u8))
            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                Penumbra.Log.Error($"Could not delete hook override file at {path}:\n{ex}");
            }

        bool? allVisible = null;
        Im.Line.Same();
        if (Im.Button("Disable All Visible Hooks"u8))
            allVisible = true;
        Im.Line.Same();
        if (Im.Button("Enable All VisibleHooks"u8))
            allVisible = false;

        bool? all = null;
        Im.Line.Same();
        if (Im.Button("Disable All Hooks"u8))
            all = true;
        Im.Line.Same();
        if (Im.Button("Enable All Hooks"u8))
            all = false;

        foreach (var propertyField in typeof(HookOverrides).GetFields().Where(f => f is { IsStatic: false, FieldType.IsValueType: true }))
        {
            using var tree = Im.Tree.Node(propertyField.Name);
            if (!tree)
            {
                if (all.HasValue)
                {
                    var property = propertyField.GetValue(_overrides);
                    foreach (var valueField in propertyField.FieldType.GetFields())
                    {
                        valueField.SetValue(property, all.Value);
                        propertyField.SetValue(_overrides, property);
                    }
                }
            }
            else
            {
                allVisible ??= all;
                var property = propertyField.GetValue(_overrides);
                foreach (var valueField in propertyField.FieldType.GetFields())
                {
                    var value = valueField.GetValue(property) as bool? ?? false;
                    if (Im.Checkbox($"Disable {valueField.Name}", ref value) || allVisible.HasValue)
                    {
                        valueField.SetValue(property, allVisible ?? value);
                        propertyField.SetValue(_overrides, property);
                    }
                }
            }
        }
    }
}
