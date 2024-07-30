using System.Collections.Frozen;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Interop.Hooks.ResourceLoading;
using Penumbra.Interop.Structs;
using Penumbra.String;

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
        _resourceLoader            =  resourceLoader;
        _processors                =  services.GetServicesImplementing<IFilePostProcessor>().ToFrozenDictionary(s => s.Type, s => s);
        _resourceLoader.FileLoaded += OnFileLoaded;
    }

    public void Dispose()
    {
        _resourceLoader.FileLoaded -= OnFileLoaded;
    }

    private void OnFileLoaded(ResourceHandle* resource, CiByteString path, bool returnValue, bool custom,
        ReadOnlySpan<byte> additionalData)
    {
        if (_processors.TryGetValue(resource->FileType, out var processor))
            processor.PostProcess(resource, path, additionalData);
    }
}
