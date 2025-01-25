using System.Collections.Frozen;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Interop.Hooks.ResourceLoading;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.Interop.Processing;

public interface IFilePostProcessor : IService
{
    public        ResourceType Type { get; }
    public unsafe void         PostProcess(ResourceHandle* resource, CiByteString originalGamePath, ReadOnlySpan<byte> additionalData);
}

public unsafe class FilePostProcessService : IRequiredService, IDisposable
{
    private readonly ResourceLoader                                     _resourceLoader;
    private readonly FrozenDictionary<ResourceType, IFilePostProcessor> _processors;

    public FilePostProcessService(ResourceLoader resourceLoader, ServiceManager services)
    {
        _resourceLoader                        =  resourceLoader;
        _processors                            =  services.GetServicesImplementing<IFilePostProcessor>().ToFrozenDictionary(s => s.Type, s => s);
        _resourceLoader.BeforeResourceComplete += OnBeforeResourceComplete;
    }

    public void Dispose()
    {
        _resourceLoader.BeforeResourceComplete -= OnBeforeResourceComplete;
    }

    private void OnBeforeResourceComplete(ResourceHandle* resource, CiByteString path, Utf8GamePath original,
        ReadOnlySpan<byte> additionalData, bool isAsync)
    {
        if (_processors.TryGetValue(resource->FileType, out var processor))
            processor.PostProcess(resource, original.Path, additionalData);
    }
}
