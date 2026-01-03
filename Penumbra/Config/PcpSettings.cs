namespace Penumbra;

public record PcpSettings
{
    public bool   CreateCollection { get; set; } = true;
    public bool   AssignCollection { get; set; } = true;
    public bool   AllowIpc         { get; set; } = true;
    public bool   DisableHandling  { get; set; } = false;
    public string FolderName       { get; set; } = "PCP";
    public string PcpExtension     { get; set; } = ".pcp";
}
