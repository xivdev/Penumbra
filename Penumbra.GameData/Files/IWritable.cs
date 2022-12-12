namespace Penumbra.GameData.Files;

public interface IWritable
{
    public bool Valid { get; }
    public byte[] Write();
}