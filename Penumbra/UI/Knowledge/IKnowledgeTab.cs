namespace Penumbra.UI.Knowledge;

public interface IKnowledgeTab
{
    public ReadOnlySpan<byte> Name       { get; }
    public ReadOnlySpan<byte> SearchTags { get; }
    public void               Draw();
}
