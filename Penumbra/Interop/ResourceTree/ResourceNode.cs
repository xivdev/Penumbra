using Penumbra.GameData.Enums;
using Penumbra.String.Classes;
using ChangedItemIcon = Penumbra.UI.ChangedItemDrawer.ChangedItemIcon;

namespace Penumbra.Interop.ResourceTree;

public class ResourceNode
{
    public readonly string?            Name;
    public readonly ChangedItemIcon    Icon;
    public readonly ResourceType       Type;
    public readonly nint               ObjectAddress;
    public readonly nint               ResourceHandle;
    public readonly Utf8GamePath       GamePath;
    public readonly Utf8GamePath[]     PossibleGamePaths;
    public readonly FullPath           FullPath;
    public readonly ulong              Length;
    public readonly bool               Internal;
    public readonly List<ResourceNode> Children;

    public ResourceNode(UIData uiData, ResourceType type, nint objectAddress, nint resourceHandle, Utf8GamePath gamePath, FullPath fullPath,
        ulong length, bool @internal)
    {
        Name           = uiData.Name;
        Icon           = uiData.Icon;
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

    public ResourceNode(UIData uiData, ResourceType type, nint objectAddress, nint resourceHandle, Utf8GamePath[] possibleGamePaths, FullPath fullPath,
        ulong length, bool @internal)
    {
        Name              = uiData.Name;
        Icon              = uiData.Icon;
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

    private ResourceNode(UIData uiData, ResourceNode originalResourceNode)
    {
        Name              = uiData.Name;
        Icon              = uiData.Icon;
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

    public ResourceNode WithUIData(string? name, ChangedItemIcon icon)
        => string.Equals(Name, name, StringComparison.Ordinal) && Icon == icon ? this : new ResourceNode(new(name, icon), this);

    public ResourceNode WithUIData(UIData uiData)
        => string.Equals(Name, uiData.Name, StringComparison.Ordinal) && Icon == uiData.Icon ? this : new ResourceNode(uiData, this);

    public readonly record struct UIData(string? Name, ChangedItemIcon Icon)
    {
        public readonly UIData PrependName(string prefix)
            => Name == null ? this : new(prefix + Name, Icon);
    }
}
