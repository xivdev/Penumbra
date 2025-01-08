using System.Collections.Immutable;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using OtterGui.Services;
using Penumbra.GameData;

namespace Penumbra.Interop.Hooks.PostProcessing;

public unsafe class RenderTargetHdrEnabler : IService, IDisposable
{
    /// <remarks> This array must be sorted by CreationOrder ascending. </remarks>
    private static readonly ImmutableArray<ForcedTextureConfig> ForcedTextureConfigs =
    [
        new(9,  TextureFormat.R16G16B16A16_FLOAT, "Main Diffuse GBuffer"),
        new(10, TextureFormat.R16G16B16A16_FLOAT, "Hair Diffuse GBuffer"),
    ];

    private static readonly IComparer<ForcedTextureConfig> ForcedTextureConfigComparer
        = Comparer<ForcedTextureConfig>.Create((lhs, rhs) => lhs.CreationOrder.CompareTo(rhs.CreationOrder));

    private readonly Configuration _config;

    private readonly ThreadLocal<TextureIndices> _textureIndices = new(() => new(-1, -1));
    private readonly ThreadLocal<Dictionary<nint, (int TextureIndex, uint TextureFormat)>?> _textures = new(() => null);

    public TextureReportRecord[]? TextureReport { get; private set; }

    [Signature(Sigs.RenderTargetManagerInitialize, DetourName = nameof(RenderTargetManagerInitializeDetour))]
    private Hook<RenderTargetManagerInitializeFunc> _renderTargetManagerInitialize = null!;

    [Signature(Sigs.DeviceCreateTexture2D, DetourName = nameof(CreateTexture2DDetour))]
    private Hook<CreateTexture2DFunc> _createTexture2D = null!;

    public RenderTargetHdrEnabler(IGameInteropProvider interop, Configuration config)
    {
        _config = config;
        interop.InitializeFromAttributes(this);
        if (config.HdrRenderTargets && !HookOverrides.Instance.PostProcessing.RenderTargetManagerInitialize)
            _renderTargetManagerInitialize.Enable();
    }

    ~RenderTargetHdrEnabler()
        => Dispose(false);

    public static ForcedTextureConfig? GetForcedTextureConfig(int creationOrder)
    {
        var i = ForcedTextureConfigs.BinarySearch(new(creationOrder, 0, string.Empty), ForcedTextureConfigComparer);
        return i >= 0 ? ForcedTextureConfigs[i] : null;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool _)
    {
        _renderTargetManagerInitialize.Disable();
        if (_createTexture2D.IsEnabled)
            _createTexture2D.Disable();

        _createTexture2D.Dispose();
        _renderTargetManagerInitialize.Dispose();
    }

    private nint RenderTargetManagerInitializeDetour(RenderTargetManager* @this)
    {
        _createTexture2D.Enable();
        _textureIndices.Value = new(0, 0);
        _textures.Value = _config.DebugMode ? [] : null;
        try
        {
            return _renderTargetManagerInitialize.Original(@this);
        }
        finally
        {
            if (_textures.Value != null)
            {
                TextureReport = CreateTextureReport(@this, _textures.Value);
                _textures.Value = null;
            }
            _textureIndices.Value = new(-1, -1);
            _createTexture2D.Disable();
        }
    }

    private Texture* CreateTexture2DDetour(
        Device* @this, int* size, byte mipLevel, uint textureFormat, uint flags, uint unk)
    {
        var originalTextureFormat = textureFormat;
        var indices = _textureIndices.IsValueCreated ? _textureIndices.Value : new(-1, -1);
        if (indices.ConfigIndex >= 0 && indices.ConfigIndex < ForcedTextureConfigs.Length &&
            ForcedTextureConfigs[indices.ConfigIndex].CreationOrder == indices.CreationOrder)
        {
            var config = ForcedTextureConfigs[indices.ConfigIndex++];
            textureFormat = (uint)config.ForcedTextureFormat;
        }

        if (indices.CreationOrder >= 0)
        {
            ++indices.CreationOrder;
            _textureIndices.Value = indices;
        }

        var texture = _createTexture2D.Original(@this, size, mipLevel, textureFormat, flags, unk);
        if (_textures.IsValueCreated)
            _textures.Value?.Add((nint)texture, (indices.CreationOrder - 1, originalTextureFormat));
        return texture;
    }

    private static TextureReportRecord[] CreateTextureReport(RenderTargetManager* renderTargetManager, Dictionary<nint, (int TextureIndex, uint TextureFormat)> textures)
    {
        var rtmTextures = new Span<nint>(renderTargetManager, sizeof(RenderTargetManager) / sizeof(nint));
        var report = new List<TextureReportRecord>();
        for (var i = 0; i < rtmTextures.Length; ++i)
        {
            if (textures.TryGetValue(rtmTextures[i], out var texture))
                report.Add(new(i * sizeof(nint), texture.TextureIndex, (TextureFormat)texture.TextureFormat));
        }
        return report.ToArray();
    }

    private delegate nint RenderTargetManagerInitializeFunc(RenderTargetManager* @this);

    private delegate Texture* CreateTexture2DFunc(Device* @this, int* size, byte mipLevel, uint textureFormat, uint flags, uint unk);

    private record struct TextureIndices(int CreationOrder, int ConfigIndex);

    public readonly record struct ForcedTextureConfig(int CreationOrder, TextureFormat ForcedTextureFormat, string Comment);

    public readonly record struct TextureReportRecord(nint Offset, int CreationOrder, TextureFormat OriginalTextureFormat);
}
