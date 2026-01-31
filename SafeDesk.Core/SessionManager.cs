using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Timers;

namespace SafeDesk.Core;

public class SessionManager
{
    private Session? _currentSession;
    private const string BaseSessionDir = @"C:\SafeDesk\sessions";
    private readonly System.Timers.Timer _lifecycleTimer;
    private const int InactivityLimitMinutes = 10;
    
    // Events for UI
    public event Action<SessionState>? StateChanged;
    public event Action<string>? LogUpdated;
    public event Action<TimeSpan>? TimeRemainingChanged;

    public Session? CurrentSession => _currentSession;

    public SessionManager()
    {
        // 1. Ensure env is safe
        CoreInitializer.InitializeSystem();

        // 2. Crash Recovery: Scan for orphans immediately
        CheckForOrphanSessions();

        // 3. Setup Lifecycle Timer (runs every second to check inactivity)
        _lifecycleTimer = new System.Timers.Timer(1000);
        _lifecycleTimer.Elapsed += LifecycleTimer_Elapsed;
        _lifecycleTimer.Start();
    }

    /// <summary>
    /// Phase 2 Recovery: Scans the session folder for any left-over data from crashes.
    /// </summary>
    private void CheckForOrphanSessions()
    {
        try
        {
            if (!Directory.Exists(BaseSessionDir)) return;

            var subDirs = Directory.GetDirectories(BaseSessionDir);
            foreach (var dir in subDirs)
            {
                // In Phase 2, ANY folder here at startup is an orphan.
                string dirName = new DirectoryInfo(dir).Name;
                AuditLogger.Log($"CRITICAL: Orphan session found: {dirName}");
                
                PerformCleanup(dir);
                
                AuditLogger.Log($"Recovered and wiped orphan session {dirName}");
            }
        }
        catch (Exception ex)
        {
            AuditLogger.Log($"Start Recovery Failed: {ex.Message}");
        }
    }

    public Session StartNewSession()
    {
        if (_currentSession != null && _currentSession.State == SessionState.Active)
        {
            throw new InvalidOperationException("A session is already active.");
        }

        Guid sessionId = Guid.NewGuid();
        string sessionPath = Path.Combine(BaseSessionDir, sessionId.ToString());

        DirectoryInfo dirInfo = Directory.CreateDirectory(sessionPath);
        ApplySecureAcl(dirInfo);

        _currentSession = new Session(sessionId, sessionPath);
        
        AuditLogger.Log($"Session {sessionId} STARTED.");
        NotifyStateChange();
        
        return _currentSession;
    }

    public void NotifyUserInteraction()
    {
        if (_currentSession != null && _currentSession.State == SessionState.Active)
        {
            _currentSession.RefreshActivity();
        }
    }

    private void LifecycleTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (_currentSession == null || _currentSession.State != SessionState.Active) return;

        var elapsed = DateTime.Now - _currentSession.LastActivity;
        var remaining = TimeSpan.FromMinutes(InactivityLimitMinutes) - elapsed;

        if (remaining <= TimeSpan.Zero)
        {
            // TIMEOUT
             _lifecycleTimer.Stop(); // Stop timer to prevent re-entry
             // We need to run this on a thread safe way if modifying state,
             // but since we are in Core, we just change state. 
             // UI needs to marshal to UI thread.
             EndSession(SessionEndReason.InactivityTimeout);
             _lifecycleTimer.Start();
        }
        else
        {
            TimeRemainingChanged?.Invoke(remaining);
        }
    }

    public void EndSession(SessionEndReason reason)
    {
        if (_currentSession == null || _currentSession.State == SessionState.Ended) return;

        AuditLogger.Log($"Session {_currentSession.Id} ENDING. Reason: {reason}");

        _currentSession.MarkEnded();
        
        // CLEANUP
        PerformCleanup(_currentSession.FolderPath);

        AuditLogger.Log($"Session {_currentSession.Id} TERMINATED & CLEARED.");
        NotifyStateChange();
    }

    private void PerformCleanup(string folderPath)
    {
        try
        {
            if (Directory.Exists(folderPath))
            {
                // In Phase 3, we will call WipeEngine here.
                // In Phase 2, we do a strictly enforced recursive delete.
                Directory.Delete(folderPath, recursive: true);
            }
        }
        catch (Exception ex)
        {
            AuditLogger.Log($"Cleanup Error for {folderPath}: {ex.Message}");
            // In a real scenario, we might retry or quarantine. 
        }
    }

    public SafeFile ImportFile(string sourceFilePath)
    {
        if (_currentSession == null || _currentSession.State != SessionState.Active)
        {
            throw new InvalidOperationException("No active session.");
        }

        // Notify activity
        _currentSession.RefreshActivity();
        
        // ... (Logic from Phase 1, kept concise here)
        string fileName = Path.GetFileName(sourceFilePath);
        string destPath = Path.Combine(_currentSession.FolderPath, fileName);
        
        if (File.Exists(destPath))
        {
             string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
             string ext = Path.GetExtension(fileName);
             destPath = Path.Combine(_currentSession.FolderPath, $"{nameWithoutExt}_{Guid.NewGuid().ToString().Substring(0, 4)}{ext}");
        }

        File.Copy(sourceFilePath, destPath);

        // Audit Import
        AuditLogger.Log($"File Imported: {Path.GetFileName(destPath)} (Size: {new FileInfo(destPath).Length} bytes)");

        var safeFile = new SafeFile
        {
            FileName = Path.GetFileName(destPath),
            SizeBytes = new FileInfo(destPath).Length,
            InternalPath = destPath
        };

        _currentSession.AddFile(safeFile);
        return safeFile;
    }

    public List<SafeFile> GetSessionFiles()
    {
        return _currentSession?.CurrentFiles ?? new List<SafeFile>();
    }

    // Pass-through for UI to see logs
    public string GetAuditLogs() => AuditLogger.GetLogs();

    private void NotifyStateChange()
    {
        StateChanged?.Invoke(_currentSession?.State ?? SessionState.Inactive);
        LogUpdated?.Invoke(AuditLogger.GetLogs());
    }

    private void ApplySecureAcl(DirectoryInfo dirInfo)
    {
        // ... (Same Phase 1 Logic)
         try
        {
            DirectorySecurity security = dirInfo.GetAccessControl();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            var currentUser = WindowsIdentity.GetCurrent().Name;
            security.AddAccessRule(new FileSystemAccessRule(currentUser, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            dirInfo.SetAccessControl(security);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ACL Error: {ex.Message}");
            throw new Exception("Failed to secure session directory.", ex);
        }
    }
}
