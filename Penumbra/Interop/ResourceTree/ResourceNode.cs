using Penumbra.Api.Enums;
using Penumbra.Mods;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.UI;
using Penumbra.UI.Classes;

namespace Penumbra.Interop.ResourceTree;

public class ResourceNode : ICloneable
{
    public          string?             Name;
    public          string?             FallbackName;
    public          ChangedItemIconFlag IconFlag;
    public readonly ResourceType        Type;
    public readonly nint                ObjectAddress;
    public readonly nint                ResourceHandle;
    public          Utf8GamePath[]      PossibleGamePaths;
    public          FullPath            FullPath;
    public          PathStatus          FullPathStatus;
    public          bool                ForceInternal;
    public          bool                ForceProtected;
    public          string?             ModName;
    public readonly WeakReference<Mod>  Mod = new(null!);
    public          string?             ModRelativePath;
    public          CiByteString        AdditionalData;
    public readonly ulong               Length;
    public readonly List<ResourceNode>  Children;
    internal        ResolveContext?     ResolveContext;

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

    /// <summary> Whether to treat the file as internal (hide from user unless debug mode is on). </summary>
    public bool Internal
        => ForceInternal || Type is ResourceType.Eid or ResourceType.Imc;

    /// <summary> Whether to treat the file as protected (require holding the Mod Deletion Modifier to make a quick import). </summary>
    public bool Protected
        => ForceProtected
         || Internal
         || Type is ResourceType.Shpk or ResourceType.Sklb or ResourceType.Skp or ResourceType.Phyb or ResourceType.Kdb or ResourceType.Pbd;

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
        IconFlag          = other.IconFlag;
        Type              = other.Type;
        ObjectAddress     = other.ObjectAddress;
        ResourceHandle    = other.ResourceHandle;
        PossibleGamePaths = other.PossibleGamePaths;
        FullPath          = other.FullPath;
        FullPathStatus    = other.FullPathStatus;
        ModName           = other.ModName;
        Mod               = other.Mod;
        ModRelativePath   = other.ModRelativePath;
        AdditionalData    = other.AdditionalData;
        ForceInternal     = other.ForceInternal;
        ForceProtected    = other.ForceProtected;
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
        Name     = uiData.Name;
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
            => Name == null ? this : this with { Name = prefix + Name };
    }

    public enum PathStatus : byte
    {
        Valid,
        NonExistent,
        External,
    }
}
