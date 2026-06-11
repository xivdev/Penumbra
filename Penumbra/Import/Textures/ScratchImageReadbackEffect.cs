using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using ImSharp;
using Luna.DirectX;
using OtterTex;
using Penumbra.Util;

namespace Penumbra.Import.Textures;

/// <summary> Base class for effects that retrieve the pixel data as a <see cref="ScratchImage"/> and process it on the CPU side. </summary>
/// <param name="readbackProvider"> Dalamud's texture readback provider. </param>
/// <seealso cref="ITextureReadbackProvider.GetAllRawImagesAsync"/>
public abstract class ScratchImageReadbackEffect(ITextureReadbackProvider readbackProvider) : WrapEffectBase
{
    /// <inheritdoc/>
    public override int Count
        => 0;

    /// <inheritdoc/>
    public override ImTextureId this[int index]
        => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override async Task Run(IDalamudTextureWrap wrap, CancellationToken cancellationToken)
    {
        using var scratch = await readbackProvider.GetScratchImageAsync(wrap, true, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await Run(scratch, cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Runs this effect. </summary>
    /// <param name="scratch"> The input texture. </param>
    /// <param name="cancellationToken"> A cancellation token. </param>
    /// <returns> A task that represents this effect running. </returns>
    protected abstract Task Run(ScratchImage scratch, CancellationToken cancellationToken);
}
