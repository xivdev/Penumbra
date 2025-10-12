using Dalamud.Plugin;
using ImSharp;
using Penumbra.Api.Api;
using Penumbra.Api.IpcSubscribers;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Api.IpcTester;

public class MetaIpcTester(IDalamudPluginInterface pi) : Luna.IUiService
{
    private int            _gameObjectIndex;
    private string         _metaBase64    = string.Empty;
    private MetaDictionary _metaDict      = new();
    private byte           _parsedVersion = byte.MaxValue;

    public void Draw()
    {
        using var _ = Im.Tree.Node("Meta"u8);
        if (!_)
            return;

        Im.Input.Scalar("##metaIdx"u8, ref _gameObjectIndex);
        if (Im.Input.Text("##metaText"u8, ref _metaBase64, "Base64 Metadata..."u8))
            _metaDict = MetaApi.ConvertManips(_metaBase64, out var m, out _parsedVersion) ? m : new MetaDictionary();


        using var table = Im.Table.Begin(StringU8.Empty, 3, TableFlags.SizingFixedFit);
        if (!table)
            return;

        IpcTester.DrawIntro(GetPlayerMetaManipulations.Label, "Player Meta Manipulations"u8);
        if (Im.Button("Copy to Clipboard##Player"u8))
        {
            var base64 = new GetPlayerMetaManipulations(pi).Invoke();
            Im.Clipboard.Set(base64);
        }

        IpcTester.DrawIntro(GetMetaManipulations.Label, "Game Object Manipulations"u8);
        if (Im.Button("Copy to Clipboard##GameObject"u8))
        {
            var base64 = new GetMetaManipulations(pi).Invoke(_gameObjectIndex);
            Im.Clipboard.Set(base64);
        }

        IpcTester.DrawIntro(string.Empty, "Parsed Data"u8);
        Im.Text($"Version: {_parsedVersion}, Count: {_metaDict.Count}");
    }
}
