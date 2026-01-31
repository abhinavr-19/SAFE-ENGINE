namespace SafeDesk.Core;

public static class CoreInitializer
{
    private static readonly string BaseDir = @"C:\SafeDesk";
    private static readonly string SessionsDir = @"C:\SafeDesk\sessions";

    public static void InitializeSystem()
    {
        try
        {
            EnsureDirectory(BaseDir);
            EnsureDirectory(SessionsDir);
        }
        catch (System.Exception ex)
        {
            // For Phase 0, we might want to just log or rethrow. 
            // In a real app we'd handle permission errors gracefully.
            System.Diagnostics.Debug.WriteLine($"Initialization failed: {ex.Message}");
            throw; 
        }
    }

    private static void EnsureDirectory(string path)
    {
        if (!System.IO.Directory.Exists(path))
        {
            System.IO.Directory.CreateDirectory(path);
        }
    }
}
