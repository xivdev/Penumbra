using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using ImSharp;
using Luna.DirectX;
using OtterTex;
using Penumbra.Util;

namespace Penumbra.Import.Textures;

/// <summary> An image processing effect that saves its input to a byte array. </summary>
/// <param name="readbackProvider"> Dalamud's texture readback provider. </param>
/// <seealso cref="ITextureReadbackProvider.GetAllRawImagesAsync"/>
public abstract class SaveToScratchImageEffect(ITextureReadbackProvider readbackProvider) : WrapEffectBase, IDisposable
{
    private ScratchImage? _scratch;

    /// <summary> The last run's image. </summary>
    public ScratchImage? ScratchImage
        => _scratch;

    /// <inheritdoc/>
    public override int Count
        => 0;

    /// <inheritdoc/>
    public override ImTextureId this[int index]
        => throw new NotSupportedException();

    ~SaveToScratchImageEffect()
        => Dispose(false);

    /// <summary> Gets the last run's image, transferring its ownership to the caller. </summary>
    public ScratchImage? DetachScratchImage()
    {
        var scratch = _scratch;
        _scratch = null;
        return scratch;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary> Releases the resources used by this object. </summary>
    /// <param name="disposing"> True if called explicitly, false if garbage collected. </param>
    protected virtual void Dispose(bool disposing)
    {
        _scratch?.Dispose();
        _scratch = null;
    }

    /// <inheritdoc/>
    protected override async Task Run(IDalamudTextureWrap wrap, CancellationToken cancellationToken)
    {
        var scratch = await readbackProvider.GetScratchImageAsync(wrap, true, cancellationToken)
            .ConfigureAwait(false);
        _scratch?.Dispose();
        _scratch = scratch;
    }
}
