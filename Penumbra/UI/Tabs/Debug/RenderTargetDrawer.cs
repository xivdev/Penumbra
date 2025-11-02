using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using ImSharp;
using OtterGui;
using OtterGui.Text;
using Penumbra.Interop.Hooks;
using Penumbra.Interop.Hooks.PostProcessing;
using Penumbra.Services;

namespace Penumbra.UI.Tabs.Debug;

public class RenderTargetDrawer(RenderTargetHdrEnabler renderTargetHdrEnabler, DalamudConfigService dalamudConfig, Configuration config) : Luna.IUiService
{
    private void DrawStatistics()
    {
        using (ImUtf8.Group())
        {
            ImUtf8.Text("Wait For Plugins (Now)");
            ImUtf8.Text("Wait For Plugins (First Launch)");

            ImUtf8.Text("HDR Enabled (Now)");
            ImUtf8.Text("HDR Enabled (First Launch)");

            ImUtf8.Text("HDR Hook Overriden (Now)");
            ImUtf8.Text("HDR Hook Overriden (First Launch)");

            ImUtf8.Text("HDR Detour Called");
            ImUtf8.Text("Penumbra Reload Count");
        }
        Im.Line.Same();
        using (ImUtf8.Group())
        {
            ImUtf8.Text($"{(dalamudConfig.GetDalamudConfig(DalamudConfigService.WaitingForPluginsOption, out bool w) ? w.ToString() : "Unknown")}");
            ImUtf8.Text($"{renderTargetHdrEnabler.FirstLaunchWaitForPluginsState?.ToString() ?? "Unknown"}");

            ImUtf8.Text($"{config.HdrRenderTargets}");
            ImUtf8.Text($"{renderTargetHdrEnabler.FirstLaunchHdrState}");

            ImUtf8.Text($"{HookOverrides.Instance.PostProcessing.RenderTargetManagerInitialize}");
            ImUtf8.Text($"{!renderTargetHdrEnabler.FirstLaunchHdrHookOverrideState}");

            ImUtf8.Text($"{renderTargetHdrEnabler.HdrEnabledSuccess}");
            ImUtf8.Text($"{renderTargetHdrEnabler.PenumbraReloadCount}");
        }
    }

    /// <summary> Draw information about render targets. </summary>
    public unsafe void Draw()
    {
        if (!ImUtf8.CollapsingHeader("Render Targets"u8))
            return;

        DrawStatistics();
        ImUtf8.Dummy(0);
        Im.Separator();
        ImUtf8.Dummy(0);
        var report = renderTargetHdrEnabler.TextureReport;
        if (report == null)
        {
            ImUtf8.Text("The RenderTargetManager report has not been gathered."u8);
            ImUtf8.Text("Please restart the game with Debug Mode and Wait for Plugins on Startup enabled to fill this section."u8);
            return;
        }

        using var table = Im.Table.Begin("##RenderTargetTable"u8, 5, TableFlags.RowBackground | TableFlags.SizingFixedFit);
        if (!table)
            return;

        table.SetupColumn("Offset"u8,                  TableColumnFlags.WidthStretch, 0.15f);
        table.SetupColumn("Creation Order"u8,          TableColumnFlags.WidthStretch, 0.15f);
        table.SetupColumn("Original Texture Format"u8, TableColumnFlags.WidthStretch, 0.2f);
        table.SetupColumn("Current Texture Format"u8,  TableColumnFlags.WidthStretch, 0.2f);
        table.SetupColumn("Comment"u8,                 TableColumnFlags.WidthStretch, 0.3f);
        ImGui.TableHeadersRow();

        foreach (var record in report)
        {
            ImUtf8.DrawTableColumn($"0x{record.Offset:X}");
            ImUtf8.DrawTableColumn($"{record.CreationOrder}");
            ImUtf8.DrawTableColumn($"{record.OriginalTextureFormat}");
            ImGui.TableNextColumn();
            var texture = *(Texture**)((nint)RenderTargetManager.Instance()
              + record.Offset);
            if (texture != null)
            {
                using var color = ImGuiColor.Text.Push(ImGuiUtil.HalfBlendText(0xFF),
                    texture->TextureFormat != record.OriginalTextureFormat);
                ImUtf8.Text($"{texture->TextureFormat}");
            }

            ImGui.TableNextColumn();
            var forcedConfig = RenderTargetHdrEnabler.GetForcedTextureConfig(record.CreationOrder);
            if (forcedConfig.HasValue)
                ImUtf8.Text(forcedConfig.Value.Comment);
        }
    }
}
