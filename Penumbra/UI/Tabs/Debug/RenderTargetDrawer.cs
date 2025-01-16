using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using ImGuiNET;
using OtterGui;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.Interop.Hooks;
using Penumbra.Interop.Hooks.PostProcessing;
using Penumbra.Services;

namespace Penumbra.UI.Tabs.Debug;

public class RenderTargetDrawer(RenderTargetHdrEnabler renderTargetHdrEnabler, DalamudConfigService dalamudConfig, Configuration config) : IUiService
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
        ImGui.SameLine();
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
        ImGui.Separator();
        ImUtf8.Dummy(0);
        var report = renderTargetHdrEnabler.TextureReport;
        if (report == null)
        {
            ImUtf8.Text("The RenderTargetManager report has not been gathered."u8);
            ImUtf8.Text("Please restart the game with Debug Mode and Wait for Plugins on Startup enabled to fill this section."u8);
            return;
        }

        using var table = ImUtf8.Table("##RenderTargetTable"u8, 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return;

        ImUtf8.TableSetupColumn("Offset"u8,                  ImGuiTableColumnFlags.WidthStretch, 0.15f);
        ImUtf8.TableSetupColumn("Creation Order"u8,          ImGuiTableColumnFlags.WidthStretch, 0.15f);
        ImUtf8.TableSetupColumn("Original Texture Format"u8, ImGuiTableColumnFlags.WidthStretch, 0.2f);
        ImUtf8.TableSetupColumn("Current Texture Format"u8,  ImGuiTableColumnFlags.WidthStretch, 0.2f);
        ImUtf8.TableSetupColumn("Comment"u8,                 ImGuiTableColumnFlags.WidthStretch, 0.3f);
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
                using var color = Dalamud.Interface.Utility.Raii.ImRaii.PushColor(ImGuiCol.Text, ImGuiUtil.HalfBlendText(0xFF),
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
