namespace SafeDesk.WipeEngine;

public interface ISecureWipeService
{
    /// <summary>
    /// Securely wipes a file from the disk.
    /// This is a placeholder for Phase 1 logic.
    /// </summary>
    /// <param name="filePath">Full path to the file.</param>
    void WipeFile(string filePath);
}

public class SecureWipeServiceStub : ISecureWipeService
{
    public void WipeFile(string filePath)
    {
        // Not implemented in Phase 0
        throw new System.NotImplementedException("Wipe logic is not implemented in Phase 0.");
    }
}
