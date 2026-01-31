using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Timers;
using SafeDesk.WipeEngine; // Added reference

namespace SafeDesk.Core;

public class SessionManager
{
    private Session? _currentSession;
    private const string BaseSessionDir = @"C:\SafeDesk\sessions";
    private readonly System.Timers.Timer _lifecycleTimer;
    private const int InactivityLimitMinutes = 10;
    private readonly ISecureWipeService _wipeService; // Injected
    private readonly SecurePrintService _printService; // Phase 4 additions

    public event Action<SessionState>? StateChanged;
    public event Action<string>? LogUpdated;
    public event Action<TimeSpan>? TimeRemainingChanged;

    public Session? CurrentSession => _currentSession;

    public SessionManager()
    {
        // Init Services
        _wipeService = new SecureWipeService();
        _printService = new SecurePrintService();

        // 1. Ensure env is safe
        CoreInitializer.InitializeSystem();

        // 2. Crash Recovery
        CheckForOrphanSessions();

        // 3. Setup Lifecycle Timer
        _lifecycleTimer = new System.Timers.Timer(1000);
        _lifecycleTimer.Elapsed += LifecycleTimer_Elapsed;
        _lifecycleTimer.Start();
    }

    private void CheckForOrphanSessions()
    {
        try
        {
            if (!Directory.Exists(BaseSessionDir)) return;

            var subDirs = Directory.GetDirectories(BaseSessionDir);
            foreach (var dir in subDirs)
            {
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
             _lifecycleTimer.Stop(); 
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
        // 1. Spool Cleanup (Best Effort)
        SecurePrintService.CleanPrintSpool();
        
        // 2. Secure Wipe (Strict)
        PerformCleanup(_currentSession.FolderPath);

        AuditLogger.Log($"Session {_currentSession.Id} TERMINATED & CLEARED.");
        NotifyStateChange();
    }

    // --- Phase 4 Additions ---

    public void PrintFile(SafeFile file)
    {
        if (_currentSession == null || _currentSession.State != SessionState.Active)
             throw new InvalidOperationException("No active session.");

        // Validation: Ensure file is really in the session folder
        if (!file.InternalPath.StartsWith(_currentSession.FolderPath, StringComparison.OrdinalIgnoreCase))
             throw new UnauthorizedAccessException("Security Violation: Attempt to print file outside session.");

        AuditLogger.Log($"Printing File: {file.FileName}...");
        _printService.PrintFile(file.InternalPath);
        _currentSession.RefreshActivity();
    }

    public SafeFile ScanDocument()
    {
        if (_currentSession == null || _currentSession.State != SessionState.Active)
             throw new InvalidOperationException("No active session.");

        // Simulate Scan Import
        string scannedFile = ScanService.SimulateScan(_currentSession.FolderPath);
        
        var safeFile = new SafeFile
        {
            FileName = Path.GetFileName(scannedFile),
            SizeBytes = new FileInfo(scannedFile).Length,
            InternalPath = scannedFile
        };
        
        _currentSession.AddFile(safeFile);
        _currentSession.RefreshActivity();
        
        AuditLogger.Log($"Document Scanned: {safeFile.FileName}");
        return safeFile;
    }

    // -------------------------

    private void PerformCleanup(string folderPath)
    {
        try
        {
            if (Directory.Exists(folderPath))
            {
                 AuditLogger.Log($"Initiating Secure Wipe for: {folderPath}");
                 
                 // Use the Wipe Engine
                 var result = _wipeService.WipeSession(folderPath);

                 if (result.Success)
                 {
                     AuditLogger.Log($"✅ Secure Wipe Complete. {result.FilesDestroyed} files overwritten and destroyed.");
                 }
                 else
                 {
                     AuditLogger.Log($"⚠️ Secure Wipe Warning: {result.Message}");
                     foreach (var err in result.Errors)
                     {
                         AuditLogger.Log($" - {err}");
                     }
                 }
            }
        }
        catch (Exception ex)
        {
            AuditLogger.Log($"Cleanup/Wipe Error for {folderPath}: {ex.Message}");
        }
    }

    public SafeFile ImportFile(string sourceFilePath)
    {
        if (_currentSession == null || _currentSession.State != SessionState.Active)
        {
            throw new InvalidOperationException("No active session.");
        }

        _currentSession.RefreshActivity();
        
        string fileName = Path.GetFileName(sourceFilePath);
        string destPath = Path.Combine(_currentSession.FolderPath, fileName);
        
        if (File.Exists(destPath))
        {
             string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
             string ext = Path.GetExtension(fileName);
             destPath = Path.Combine(_currentSession.FolderPath, $"{nameWithoutExt}_{Guid.NewGuid().ToString().Substring(0, 4)}{ext}");
        }

        File.Copy(sourceFilePath, destPath);

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

    public string GetAuditLogs() => AuditLogger.GetLogs();

    private void NotifyStateChange()
    {
        StateChanged?.Invoke(_currentSession?.State ?? SessionState.Inactive);
        LogUpdated?.Invoke(AuditLogger.GetLogs());
    }

    private void ApplySecureAcl(DirectoryInfo dirInfo)
    {
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
