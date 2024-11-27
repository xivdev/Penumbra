using Dalamud.Plugin;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.Api.Api;
using Penumbra.Api.IpcSubscribers;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Api.IpcTester;

public class MetaIpcTester(IDalamudPluginInterface pi) : IUiService
{
    private int            _gameObjectIndex;
    private string         _metaBase64    = string.Empty;
    private MetaDictionary _metaDict      = new();
    private byte           _parsedVersion = byte.MaxValue;

    public void Draw()
    {
        using var _ = ImRaii.TreeNode("Meta");
        if (!_)
            return;

        ImGui.InputInt("##metaIdx", ref _gameObjectIndex, 0, 0);
        if (ImUtf8.InputText("##metaText"u8, ref _metaBase64, "Base64 Metadata..."u8))
            if (!MetaApi.ConvertManips(_metaBase64, out _metaDict!, out _parsedVersion))
                _metaDict ??= new MetaDictionary();


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

        IpcTester.DrawIntro(string.Empty, "Parsed Data");
        ImUtf8.Text($"Version: {_parsedVersion}, Count: {_metaDict.Count}");
    }
}
