namespace SafeDesk.WipeEngine;

public class SecureWipeResult
{
    public bool Success { get; set; }
    public int FilesDestroyed { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
    public string Message { get; set; } = string.Empty;
}

public interface ISecureWipeService
{
    /// <summary>
    /// Securely destroys a session directory and all its contents.
    /// </summary>
    /// <param name="sessionPath">Full path to the session folder.</param>
    /// <returns>Result of the operation.</returns>
    SecureWipeResult WipeSession(string sessionPath);
}
