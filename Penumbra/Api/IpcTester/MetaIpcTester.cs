using Dalamud.Plugin;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui.Services;
using Penumbra.Api.IpcSubscribers;

namespace Penumbra.Api.IpcTester;

public class MetaIpcTester(DalamudPluginInterface pi) : IUiService
{
    private int _gameObjectIndex;

    public void Draw()
    {
        using var _ = ImRaii.TreeNode("Meta");
        if (!_)
            return;

        ImGui.InputInt("##metaIdx", ref _gameObjectIndex, 0, 0);
        using var table = ImRaii.Table(string.Empty, 3, ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return;

        IpcTester.DrawIntro(GetPlayerMetaManipulations.Label, "Player Meta Manipulations");
        if (ImGui.Button("Copy to Clipboard##Player"))
        {
            var base64 = new GetPlayerMetaManipulations(pi).Invoke();
            ImGui.SetClipboardText(base64);
        }

        IpcTester.DrawIntro(GetMetaManipulations.Label, "Game Object Manipulations");
        if (ImGui.Button("Copy to Clipboard##GameObject"))
        {
            var base64 = new GetMetaManipulations(pi).Invoke(_gameObjectIndex);
            ImGui.SetClipboardText(base64);
        }
    }
}
