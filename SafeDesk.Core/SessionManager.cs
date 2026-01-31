using System.Security.AccessControl;
using System.Security.Principal;

namespace SafeDesk.Core;

public class Session
{
    public Guid Id { get; private set; }
    public string FolderPath { get; private set; }
    public DateTime StartTime { get; private set; }
    public bool IsActive { get; private set; }
    // We only expose metadata to the UI, not full paths that can be manipulated easily
    public List<SafeFile> CurrentFiles { get; private set; }

    public Session(Guid id, string folderPath)
    {
        Id = id;
        FolderPath = folderPath;
        StartTime = DateTime.Now;
        IsActive = true;
        CurrentFiles = new List<SafeFile>();
    }

    public void End()
    {
        IsActive = false;
    }

    public void AddFile(SafeFile file)
    {
        CurrentFiles.Add(file);
    }
}

public class SafeFile
{
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    // Internal path for Core use only
    internal string InternalPath { get; set; } = string.Empty;
}

public class SessionManager
{
    private Session? _currentSession;
    private const string BaseSessionDir = @"C:\SafeDesk\sessions";

    public Session? CurrentSession => _currentSession;

    public Session StartNewSession()
    {
        if (_currentSession != null && _currentSession.IsActive)
        {
            throw new InvalidOperationException("A session is already active.");
        }

        Guid sessionId = Guid.NewGuid();
        string sessionPath = Path.Combine(BaseSessionDir, sessionId.ToString());

        // 1. Create Directory
        DirectoryInfo dirInfo = Directory.CreateDirectory(sessionPath);

        // 2. Apply NTFS Security (Zero-Trust)
        ApplySecureAcl(dirInfo);

        _currentSession = new Session(sessionId, sessionPath);
        return _currentSession;
    }

    public void EndSession()
    {
        if (_currentSession == null) return;
        _currentSession.End();
        // Setup for next session, but keep _currentSession for display until new one starts or app closes
    }

    public SafeFile ImportFile(string sourceFilePath)
    {
        if (_currentSession == null || !_currentSession.IsActive)
        {
            throw new InvalidOperationException("No active session.");
        }

        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("Source file not found.", sourceFilePath);
        }

        string fileName = Path.GetFileName(sourceFilePath);
        string destPath = Path.Combine(_currentSession.FolderPath, fileName);

        // Prevent overwriting or path traversal
        if (File.Exists(destPath))
        {
            // Simple unique naming strategy
            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            destPath = Path.Combine(_currentSession.FolderPath, $"{nameWithoutExt}_{Guid.NewGuid().ToString().Substring(0, 4)}{ext}");
        }

        // COPY the file (Isolating customer data)
        File.Copy(sourceFilePath, destPath);

        var fileInfo = new FileInfo(destPath);
        var safeFile = new SafeFile
        {
            FileName = fileInfo.Name,
            SizeBytes = fileInfo.Length,
            InternalPath = destPath
        };

        _currentSession.AddFile(safeFile);
        return safeFile;
    }

    public List<SafeFile> GetSessionFiles()
    {
        return _currentSession?.CurrentFiles ?? new List<SafeFile>();
    }

    private void ApplySecureAcl(DirectoryInfo dirInfo)
    {
        try
        {
            DirectorySecurity security = dirInfo.GetAccessControl();

            // Disable inheritance, remove all inherited rules
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            // Add Rule: Current User (Full Control) - needed for the app running as user
            var currentUser = WindowsIdentity.GetCurrent().Name;
            security.AddAccessRule(new FileSystemAccessRule(
                currentUser,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            // Add Rule: System (Full Control) - for OS operations
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            dirInfo.SetAccessControl(security);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ACL Error: {ex.Message}");
            throw new Exception("Failed to secure session directory. Security policy enforcement failed.", ex);
        }
    }
}
