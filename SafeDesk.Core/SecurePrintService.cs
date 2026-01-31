using System;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;

namespace SafeDesk.Core;

public class SecurePrintService
{
    public void PrintFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found to print.");

        // In a real app, strict file type checking would happen here.
        // For Phase 4, we support Text files natively via PrintDocument,
        // and images via Drawing.
        // Complex files (PDF) would typically require a PDF library or launching a process.
        // Here we implement a native text/basic image printer to satisfy "No external apps if possible" 
        // or fallback to Process.Start for others.

        string ext = Path.GetExtension(filePath).ToLower();

        if (ext == ".txt" || ext == ".log" || ext == ".cs" || ext == ".xml")
        {
            PrintTextFile(filePath);
        }
        else if (ext == ".jpg" || ext == ".png" || ext == ".bmp")
        {
            PrintImageFile(filePath);
        }
        else
        {
             // Fallback: Use Shell Execute "print" verb (Controlled)
             // This is risky for "Escape Prevention" if the app allows Save As,
             // but standard for generic file printing without libraries.
             PrintViaShell(filePath);
        }
    }

    private void PrintTextFile(string filePath)
    {
        try
        {
            string textToPrint = File.ReadAllText(filePath);
            var printFont = new Font("Arial", 10);
            var printDoc = new PrintDocument();
            printDoc.DocumentName = $"SafeDesk Secure Print - {Path.GetFileName(filePath)}";
            
            printDoc.PrintPage += (s, e) =>
            {
                float linesPerPage = 0;
                float yPos = 0;
                int count = 0;
                float leftMargin = e.MarginBounds.Left;
                float topMargin = e.MarginBounds.Top;
                string line = null!;
                
                // Simple Logic (Full text dump, not paginated for brevity in this MVP phase)
                // Just drawing the string
                e.Graphics!.DrawString(textToPrint, printFont, Brushes.Black, leftMargin, topMargin, new StringFormat());
            };

            printDoc.Print();
        }
        catch (Exception ex)
        {
            AuditLogger.Log($"Print Error (Text): {ex.Message}");
            throw;
        }
    }

    private void PrintImageFile(string filePath)
    {
        try
        {
            var printDoc = new PrintDocument();
            printDoc.DocumentName = $"SafeDesk Secure Print - {Path.GetFileName(filePath)}";
            
            printDoc.PrintPage += (s, e) =>
            {
                using (Image img = Image.FromFile(filePath))
                {
                    // Scale to fit
                    Rectangle m = e.MarginBounds;
                    if ((double)img.Width / (double)img.Height > (double)m.Width / (double)m.Height) // image is wider
                    {
                        m.Height = (int)((double)img.Height / (double)img.Width * (double)m.Width);
                    }
                    else
                    {
                        m.Width = (int)((double)img.Width / (double)img.Height * (double)m.Height);
                    }
                    e.Graphics!.DrawImage(img, m);
                }
            };
            printDoc.Print();
        }
        catch (Exception ex)
        {
             AuditLogger.Log($"Print Error (Image): {ex.Message}");
             throw;
        }
    }

    private void PrintViaShell(string filePath)
    {
        var procInfo = new System.Diagnostics.ProcessStartInfo()
        {
            Verb = "print",
            FileName = filePath,
            CreateNoWindow = true,
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
            UseShellExecute = true
        };

        try
        {
            using (var proc = System.Diagnostics.Process.Start(procInfo))
            {
                proc?.WaitForExit(10000); // Wait max 10s
            }
        }
        catch (Exception ex)
        {
            AuditLogger.Log($"Print Error (Shell): {ex.Message}");
            throw new Exception("Could not print file type securely.");
        }
    }

    public static void CleanPrintSpool()
    {
        // Best-effort spool cleanup
        string spoolDir = @"C:\Windows\System32\spool\PRINTERS";
        try
        {
            if (Directory.Exists(spoolDir))
            {
                var files = Directory.GetFiles(spoolDir);
                foreach (var f in files)
                {
                    try { File.Delete(f); } catch { }
                }
                AuditLogger.Log("Print Spool Cleanup attempted.");
            }
        }
        catch (Exception ex)
        {
             AuditLogger.Log($"Print Spool Cleanup failed (Access Denied?): {ex.Message}");
        }
    }
}
