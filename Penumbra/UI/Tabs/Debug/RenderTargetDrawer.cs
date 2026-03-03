using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using ImSharp;
using Penumbra.Interop.Hooks;
using Penumbra.Interop.Hooks.PostProcessing;
using Penumbra.Services;

namespace Penumbra.UI.Tabs.Debug;

public class RenderTargetDrawer(RenderTargetHdrEnabler renderTargetHdrEnabler, DalamudConfigService dalamudConfig, Configuration config)
    : Luna.IUiService
{
    private void DrawStatistics()
    {
        using (Im.Group())
        {
            Im.Text("Wait For Plugins (Now)"u8);
            Im.Text("Wait For Plugins (First Launch)"u8);

            Im.Text("HDR Enabled (Now)"u8);
            Im.Text("HDR Enabled (First Launch)"u8);

            Im.Text("HDR Hook Overriden (Now)"u8);
            Im.Text("HDR Hook Overriden (First Launch)"u8);

            Im.Text("HDR Detour Called"u8);
            Im.Text("Penumbra Reload Count"u8);
        }

        Im.Line.Same();
        using (Im.Group())
        {
            Im.Text($"{(dalamudConfig.GetDalamudConfig(DalamudConfigService.WaitingForPluginsOption, out bool w) ? w.ToString() : "Unknown")}");
            Im.Text($"{renderTargetHdrEnabler.FirstLaunchWaitForPluginsState?.ToString() ?? "Unknown"}");

            Im.Text($"{config.HdrRenderTargets}");
            Im.Text($"{renderTargetHdrEnabler.FirstLaunchHdrState}");

            Im.Text($"{HookOverrides.Instance.PostProcessing.RenderTargetManagerInitialize}");
            Im.Text($"{!renderTargetHdrEnabler.FirstLaunchHdrHookOverrideState}");

            Im.Text($"{renderTargetHdrEnabler.HdrEnabledSuccess}");
            Im.Text($"{renderTargetHdrEnabler.PenumbraReloadCount}");
        }
    }

    /// <summary> Draw information about render targets. </summary>
    public unsafe void Draw()
    {
        if (!Im.Tree.Header("Render Targets"u8))
            return;

        DrawStatistics();
        Im.Dummy(0);
        Im.Separator();
        Im.Dummy(0);
        var report = renderTargetHdrEnabler.TextureReport;
        if (report is null)
        {
            Im.Text("The RenderTargetManager report has not been gathered."u8);
            Im.Text("Please restart the game with Debug Mode and Wait for Plugins on Startup enabled to fill this section."u8);
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
        table.HeaderRow();

        foreach (var record in report)
        {
            table.DrawColumn($"0x{record.Offset:X}");
            table.DrawColumn($"{record.CreationOrder}");
            table.DrawColumn($"{record.OriginalTextureFormat}");
            table.NextColumn();
            var texture = *(Texture**)((nint)RenderTargetManager.Instance()
              + record.Offset);
            if (texture is not null)
            {
                using var color = ImGuiColor.Text.Push(ImGuiColor.Text.Get().HalfBlend(Rgba32.Red),
                    texture->TextureFormat != record.OriginalTextureFormat);
                Im.Text($"{texture->TextureFormat}");
            }

            table.NextColumn();
            var forcedConfig = RenderTargetHdrEnabler.GetForcedTextureConfig(record.CreationOrder);
            if (forcedConfig.HasValue)
                Im.Text(forcedConfig.Value.Comment);
        }
    }
}
