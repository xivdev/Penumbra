using System;
using System.Collections.Generic;
using Penumbra.GameData.Enums;
using Penumbra.String.Classes;

namespace Penumbra.Interop.ResourceTree;

public class ResourceNode
{
    public readonly string?            Name;
    public readonly ResourceType       Type;
    public readonly nint               SourceAddress;
    public readonly Utf8GamePath       GamePath;
    public readonly Utf8GamePath[]     PossibleGamePaths;
    public readonly FullPath           FullPath;
    public readonly bool               Internal;
    public readonly List<ResourceNode> Children;

    public ResourceNode(string? name, ResourceType type, nint sourceAddress, Utf8GamePath gamePath, FullPath fullPath, bool @internal)
    {
        Name          = name;
        Type          = type;
        SourceAddress = sourceAddress;
        GamePath      = gamePath;
        PossibleGamePaths = new[]
        {
            gamePath,
        };
        FullPath = fullPath;
        Internal = @internal;
        Children = new List<ResourceNode>();
    }

    public ResourceNode(string? name, ResourceType type, nint sourceAddress, Utf8GamePath[] possibleGamePaths, FullPath fullPath,
        bool @internal)
    {
        Name              = name;
        Type              = type;
        SourceAddress     = sourceAddress;
        GamePath          = possibleGamePaths.Length == 1 ? possibleGamePaths[0] : Utf8GamePath.Empty;
        PossibleGamePaths = possibleGamePaths;
        FullPath          = fullPath;
        Internal          = @internal;
        Children          = new List<ResourceNode>();
    }

    private ResourceNode(string? name, ResourceNode originalResourceNode)
    {
        Name              = name;
        Type              = originalResourceNode.Type;
        SourceAddress     = originalResourceNode.SourceAddress;
        GamePath          = originalResourceNode.GamePath;
        PossibleGamePaths = originalResourceNode.PossibleGamePaths;
        FullPath          = originalResourceNode.FullPath;
        Internal          = originalResourceNode.Internal;
        Children          = originalResourceNode.Children;
    }

    public ResourceNode WithName(string? name)
        => string.Equals(Name, name, StringComparison.Ordinal) ? this : new ResourceNode(name, this);
}
