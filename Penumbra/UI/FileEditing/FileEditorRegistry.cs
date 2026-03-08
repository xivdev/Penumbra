using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using Penumbra.Api.Enums;
using Penumbra.UI.FileEditing.Materials;
using Penumbra.UI.FileEditing.Models;
using Penumbra.UI.FileEditing.Shaders;
using Penumbra.UI.FileEditing.Skeletons;
using Penumbra.UI.FileEditing.Textures;

namespace Penumbra.UI.FileEditing;

/// <remarks> While not nominally implementing <see cref="IFileEditorFactory"/>, this structurally implements as much of it as reasonable. </remarks>
public class FileEditorRegistry : Luna.IUiService
{
    private readonly Configuration _config;

    private readonly Dictionary<IFileEditorFactory, FactoryMetadata> _factories = [];

    private readonly Dictionary<string, IFileEditorFactory>             _factoriesByIdentifier = [];
    private readonly Dictionary<ResourceType, List<IFileEditorFactory>> _factoriesByType       = [];
    private readonly List<IFileEditorFactory>                           _genericFactories      = [];

    public IEnumerable<ResourceType>? SupportedResourceTypes
        => _genericFactories.Count > 0
            ? null
            : from entry in _factoriesByType where entry.Value.Count > 0 select entry.Key;

    public FileEditorRegistry(Configuration config, MaterialEditorFactory materialEditorFactory, ModelEditorFactory modelEditorFactory,
        ShaderPackageEditorFactory shaderPackageEditorFactory, DeformerEditorFactory deformerEditorFactory,
        CombiningTextureEditorFactory textureEditorFactory)
    {
        _config = config;

        RegisterFactory(materialEditorFactory);
        RegisterFactory(modelEditorFactory);
        RegisterFactory(shaderPackageEditorFactory);
        RegisterFactory(deformerEditorFactory);
        RegisterFactory(textureEditorFactory);
    }

    public void RegisterFactory(IFileEditorFactory editorFactory)
    {
        var metadata = FactoryMetadata.FromFactory(editorFactory);
        _factoriesByIdentifier.Add(metadata.Identifier, editorFactory);
        try
        {
            _factories.Add(editorFactory, metadata);
            try
            {
                switch (metadata.SupportedResourceTypes)
                {
                    case null: _genericFactories.Add(editorFactory); break;
                    case var supportedTypes:
                        foreach (var type in supportedTypes)
                        {
                            if (!_factoriesByType.TryGetValue(type, out var factories))
                            {
                                factories = [];
                                _factoriesByType.Add(type, factories);
                            }

                            factories.Add(editorFactory);
                        }

                        break;
                }
            }
            catch
            {
                UnregisterFactory(editorFactory);
                throw;
            }
        }
        catch
        {
            _factoriesByIdentifier.Remove(metadata.Identifier);
            throw;
        }
    }

    public void UnregisterFactory(IFileEditorFactory editorFactory)
    {
        if (!_factories.TryGetValue(editorFactory, out var metadata))
            return;

        switch (metadata.SupportedResourceTypes)
        {
            case null: _genericFactories.Remove(editorFactory); break;
            case var supportedTypes:
                foreach (var type in supportedTypes)
                {
                    if (_factoriesByType.TryGetValue(type, out var factories))
                        factories.Remove(editorFactory);
                }

                break;
        }

        _factories.Remove(editorFactory);
        _factoriesByIdentifier.Remove(metadata.Identifier);
    }

    public IFileEditorFactory GetFactoryByIdentifier(string identifier)
        => _factoriesByIdentifier[identifier];

    public bool TryGetFactoryByIdentifier(string identifier, [NotNullWhen(true)] out IFileEditorFactory? editorFactory)
        => _factoriesByIdentifier.TryGetValue(identifier, out editorFactory);

    private IEnumerable<IFileEditorFactory> GetFactoriesForType(ResourceType type)
        => _factoriesByType.TryGetValue(type, out var factories)
            ? factories.Concat(_genericFactories)
            : _genericFactories;

    private IFileEditorFactory GetPreferred(ResourceType type, IEnumerable<IFileEditorFactory> factories)
    {
        if (_config.PreferredEditorFactories.TryGetValue(type, out var preferred))
        {
            IFileEditorFactory? first = null;
            foreach (var factory in factories)
            {
                if (factory.Identifier == preferred)
                    return factory;
                first ??= factory;
            }

            return first ?? throw new InvalidOperationException("No suitable editor factory found for the given file");
        }
        else
        {
            return factories.First();
        }
    }

    public bool SupportsFile(string path, string? gamePath)
        => GetFactoriesForFile(path, gamePath).Any();

    public IFileEditor CreateForFile(string path, bool writable, string? gamePath, FileEditingContext? context)
        => GetPreferred(ResourceType.FromPath(path), GetFactoriesForFile(path, gamePath))
            .CreateForFile(path, writable, gamePath, context);

    public IEnumerable<IFileEditorFactory> GetFactoriesForFile(string path, string? gamePath)
        => GetFactoriesForType(ResourceType.FromPath(path)).Where(factory => factory.SupportsFile(path, gamePath));

    public bool SupportsGameFile(string path)
        => GetFactoriesForGameFile(path).Any();

    public IFileEditor CreateForGameFile(string path, FileEditingContext? context)
        => GetPreferred(ResourceType.FromPath(path), GetFactoriesForGameFile(path)).CreateForGameFile(path, context);

    public IEnumerable<IFileEditorFactory> GetFactoriesForGameFile(string path)
        => GetFactoriesForType(ResourceType.FromPath(path)).Where(factory => factory.SupportsGameFile(path));

    public unsafe bool SupportsResourceHandle(ResourceHandle* handle, string? gamePath)
        => GetFactoriesForResourceHandle(handle, gamePath).Any();

    public unsafe IFileEditor CreateForResourceHandle(ResourceHandle* handle, string? gamePath, FileEditingContext? context)
        => GetPreferred((ResourceType)handle->FileType, GetFactoriesForResourceHandle(handle, gamePath))
            .CreateForResourceHandle(handle, gamePath, context);

    public unsafe IEnumerable<IFileEditorFactory> GetFactoriesForResourceHandle(ResourceHandle* handle, string? gamePath)
        => GetFactoriesForType((ResourceType)handle->FileType).Where(factory => factory.SupportsResourceHandle(handle, gamePath));

    private readonly record struct FactoryMetadata(string Identifier, ResourceType[]? SupportedResourceTypes)
    {
        public static FactoryMetadata FromFactory(IFileEditorFactory factory)
            => new(factory.Identifier, factory.SupportedResourceTypes?.ToArray());
    }
}
