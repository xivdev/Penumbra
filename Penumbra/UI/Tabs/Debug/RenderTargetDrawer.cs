using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using ImGuiNET;
using OtterGui;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.Interop.Hooks.PostProcessing;

namespace Penumbra.UI.Tabs.Debug;

public class RenderTargetDrawer(RenderTargetHdrEnabler renderTargetHdrEnabler) : IUiService
{
    /// <summary> Draw information about render targets. </summary>
    public unsafe void Draw()
    {
        if (!ImUtf8.CollapsingHeader("Render Targets"u8))
            return;

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
