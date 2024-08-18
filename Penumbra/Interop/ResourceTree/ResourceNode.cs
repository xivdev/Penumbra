using Penumbra.Api.Enums;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.UI;

namespace Penumbra.Interop.ResourceTree;

public class ResourceNode : ICloneable
{
    public          string?            Name;
    public          string?            FallbackName;
    public          ChangedItemIconFlag    IconFlag;
    public readonly ResourceType       Type;
    public readonly nint               ObjectAddress;
    public readonly nint               ResourceHandle;
    public          Utf8GamePath[]     PossibleGamePaths;
    public          FullPath           FullPath;
    public          string?            ModName;
    public          string?            ModRelativePath;
    public          CiByteString       AdditionalData;
    public readonly ulong              Length;
    public readonly List<ResourceNode> Children;
    internal        ResolveContext?    ResolveContext;

    public Utf8GamePath GamePath
    {
        get => PossibleGamePaths.Length == 1 ? PossibleGamePaths[0] : Utf8GamePath.Empty;
        set
        {
            if (value.IsEmpty)
                PossibleGamePaths = [];
            else
                PossibleGamePaths = [value];
        }
    }

    public bool Internal
        => Type is ResourceType.Eid or ResourceType.Imc;

    internal ResourceNode(ResourceType type, nint objectAddress, nint resourceHandle, ulong length, ResolveContext? resolveContext)
    {
        Type              = type;
        ObjectAddress     = objectAddress;
        ResourceHandle    = resourceHandle;
        PossibleGamePaths = [];
        AdditionalData    = CiByteString.Empty;
        Length            = length;
        Children          = new List<ResourceNode>();
        ResolveContext    = resolveContext;
    }

    private ResourceNode(ResourceNode other)
    {
        Name              = other.Name;
        FallbackName      = other.FallbackName;
        IconFlag              = other.IconFlag;
        Type              = other.Type;
        ObjectAddress     = other.ObjectAddress;
        ResourceHandle    = other.ResourceHandle;
        PossibleGamePaths = other.PossibleGamePaths;
        FullPath          = other.FullPath;
        ModName           = other.ModName;
        ModRelativePath   = other.ModRelativePath;
        AdditionalData    = other.AdditionalData;
        Length            = other.Length;
        Children          = other.Children;
        ResolveContext    = other.ResolveContext;
    }

    public ResourceNode Clone()
        => new(this);

    object ICloneable.Clone()
        => Clone();

    public void ProcessPostfix(Action<ResourceNode, ResourceNode?> action, ResourceNode? parent)
    {
        foreach (var child in Children)
            child.ProcessPostfix(action, this);
        action(this, parent);
    }

    public void SetUiData(UiData uiData)
    {
        Name = uiData.Name;
        IconFlag = uiData.IconFlag;
    }

    public void PrependName(string prefix)
    {
        if (Name != null)
            Name = prefix + Name;
    }

    public readonly record struct UiData(string? Name, ChangedItemIconFlag IconFlag)
    {
        public UiData PrependName(string prefix)
            => Name == null ? this : new UiData(prefix + Name, IconFlag);
    }
}
