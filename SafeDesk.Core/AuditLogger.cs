using System;
using System.IO;

namespace SafeDesk.Core;

public static class AuditLogger
{
    private static readonly string LogDir = @"C:\SafeDesk\logs";
    private static readonly string LogFile = Path.Combine(LogDir, "audit.log");

    static AuditLogger()
    {
        if (!Directory.Exists(LogDir))
        {
            Directory.CreateDirectory(LogDir);
        }
    }

    public static void Log(string message)
    {
        try
        {
            string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(LogFile, entry);
        }
        catch (Exception)
        {
            // Fail silently in Phase 2 if logging fails (e.g. disk full), 
            // ensuring it doesn't crash the main app flow.
        }
    }

    public static string GetLogs()
    {
        if (File.Exists(LogFile))
        {
            // Return last 50 lines or full content
            return File.ReadAllText(LogFile);
        }
        return "No logs found.";
    }
}
