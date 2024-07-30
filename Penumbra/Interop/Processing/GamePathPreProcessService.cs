using System.Collections.Frozen;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.Interop.Processing;

public interface IPathPreProcessor : IService
{
    public ResourceType Type { get; }

    public FullPath? PreProcess(ResolveData resolveData, CiByteString path, Utf8GamePath originalGamePath, bool nonDefault, FullPath? resolved);
}

public class GamePathPreProcessService : IService
{
    private readonly FrozenDictionary<ResourceType, IPathPreProcessor> _processors;

    public GamePathPreProcessService(ServiceManager services)
    {
        _processors = services.GetServicesImplementing<IPathPreProcessor>().ToFrozenDictionary(s => s.Type, s => s);
    }


    public (FullPath? Path, ResolveData Data) PreProcess(ResolveData resolveData, CiByteString path, bool nonDefault, ResourceType type,
        FullPath? resolved,
        Utf8GamePath originalPath)
    {
        if (!_processors.TryGetValue(type, out var processor))
            return (resolved, resolveData);

        resolved = processor.PreProcess(resolveData, path, originalPath, nonDefault, resolved);
        return (resolved, resolveData);
    }
}
