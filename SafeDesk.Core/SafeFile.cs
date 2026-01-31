namespace SafeDesk.Core;

public class SafeFile
{
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    // Internal path for Core use only
    internal string InternalPath { get; set; } = string.Empty;
}
