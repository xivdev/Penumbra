using System;
using System.Collections.Generic;
using Penumbra.GameData.Enums;
using Penumbra.String.Classes;

namespace Penumbra.Interop.ResourceTree;

public class ResourceNode
{
    public readonly string?            Name;
    public readonly ResourceType       Type;
    public readonly nint               ObjectAddress;
    public readonly nint               ResourceHandle;
    public readonly Utf8GamePath       GamePath;
    public readonly Utf8GamePath[]     PossibleGamePaths;
    public readonly FullPath           FullPath;
    public readonly ulong              Length;
    public readonly bool               Internal;
    public readonly List<ResourceNode> Children;

    public ResourceNode(string? name, ResourceType type, nint objectAddress, nint resourceHandle, Utf8GamePath gamePath, FullPath fullPath, ulong length, bool @internal)
    {
        Name           = name;
        Type           = type;
        ObjectAddress  = objectAddress;
        ResourceHandle = resourceHandle;
        GamePath       = gamePath;
        PossibleGamePaths = new[]
        {
            gamePath,
        };
        FullPath = fullPath;
        Length   = length;
        Internal = @internal;
        Children = new List<ResourceNode>();
    }

    public ResourceNode(string? name, ResourceType type, nint objectAddress, nint resourceHandle, Utf8GamePath[] possibleGamePaths, FullPath fullPath,
        ulong length, bool @internal)
    {
        Name              = name;
        Type              = type;
        ObjectAddress     = objectAddress;
        ResourceHandle    = resourceHandle;
        GamePath          = possibleGamePaths.Length == 1 ? possibleGamePaths[0] : Utf8GamePath.Empty;
        PossibleGamePaths = possibleGamePaths;
        FullPath          = fullPath;
        Length            = length;
        Internal          = @internal;
        Children          = new List<ResourceNode>();
    }

    private ResourceNode(string? name, ResourceNode originalResourceNode)
    {
        Name              = name;
        Type              = originalResourceNode.Type;
        ObjectAddress     = originalResourceNode.ObjectAddress;
        ResourceHandle    = originalResourceNode.ResourceHandle;
        GamePath          = originalResourceNode.GamePath;
        PossibleGamePaths = originalResourceNode.PossibleGamePaths;
        FullPath          = originalResourceNode.FullPath;
        Length            = originalResourceNode.Length;
        Internal          = originalResourceNode.Internal;
        Children          = originalResourceNode.Children;
    }

    public ResourceNode WithName(string? name)
        => string.Equals(Name, name, StringComparison.Ordinal) ? this : new ResourceNode(name, this);
}
