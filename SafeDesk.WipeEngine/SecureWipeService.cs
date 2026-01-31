using System.Security.Cryptography;
using System.Text;

namespace SafeDesk.WipeEngine;

public class SecureWipeService : ISecureWipeService
{
    private const int BufferSize = 4096; // 4KB buffer
    private const int OverwritePasses = 1; // Minimum 1 pass for speed/security balance in Phase 3

    public SecureWipeResult WipeSession(string sessionPath)
    {
        var result = new SecureWipeResult { Success = true, FilesDestroyed = 0 };
        
        if (!Directory.Exists(sessionPath))
        {
            result.Message = "Session path not found (already deleted?)";
            return result;
        }

        try
        {
            // 1. Wipe all files
            var files = Directory.GetFiles(sessionPath, "*.*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                try
                {
                    WipeFile(file);
                    result.FilesDestroyed++;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Errors.Add($"Failed to wipe {Path.GetFileName(file)}: {ex.Message}");
                }
            }

            // 2. Wipe sub-directories (Empty them, rename, delete)
            var dirs = Directory.GetDirectories(sessionPath, "*", SearchOption.AllDirectories);
            // Sort by length descending to delete children before parents
            Array.Sort(dirs, (a, b) => b.Length.CompareTo(a.Length));

            foreach (var dir in dirs)
            {
                try
                {
                    Directory.Delete(dir, false);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Failed to delete directory {Path.GetFileName(dir)}: {ex.Message}");
                }
            }

            // 3. Delete root session folder
            Directory.Delete(sessionPath, false);
            
            if (result.Success)
            {
                result.Message = "Secure wipe completed successfully.";
            }
            else
            {
                result.Message = "Secure wipe completed with errors.";
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Critical Wipe Error: {ex.Message}");
            result.Message = "Critical failure during wipe process.";
        }

        return result;
    }

    private void WipeFile(string filePath)
    {
        FileInfo fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists) return;

        // Reset attributes to ensure we can write
        File.SetAttributes(filePath, FileAttributes.Normal);

        long length = fileInfo.Length;

        // 1. Overwrite with Random Data
        using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            byte[] buffer = new byte[BufferSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                long bytesWritten = 0;
                while (bytesWritten < length)
                {
                    int bytesToWrite = (int)Math.Min(BufferSize, length - bytesWritten);
                    rng.GetBytes(buffer, 0, bytesToWrite);
                    stream.Write(buffer, 0, bytesToWrite);
                    bytesWritten += bytesToWrite;
                }
            }
            stream.Flush(true); // Force write to disk
        }

        // 2. Rename to random string (Obfuscate original filename in MFT)
        string directory = Path.GetDirectoryName(filePath)!;
        string randomName = Path.GetRandomFileName(); // Returns cryptographically strong random 8.3 string
        string newPath = Path.Combine(directory, randomName);
        
        File.Move(filePath, newPath);

        // 3. Delete the renamed file
        File.Delete(newPath);
    }
}
