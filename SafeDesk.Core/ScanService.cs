using System;
using System.Drawing;
using System.IO;

namespace SafeDesk.Core;

public class ScanService
{
    public static string SimulateScan(string sessionPath)
    {
        // In a real implementation, this would interface with WIA/TWAIN devices.
        // For Phase 4, we simulate an incoming scan file appearing in the session.
        
        string fileName = $"Scanned_Doc_{DateTime.Now:HHmmss}.jpg";
        string filePath = Path.Combine(sessionPath, fileName);

        // Generate a dummy "Scanned" Image
        using (Bitmap bmp = new Bitmap(800, 1000))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            g.DrawString("SafeDesk Secure Scan", new Font("Arial", 24, FontStyle.Bold), Brushes.DarkBlue, 50, 50);
            g.DrawString($"Session ID: {new DirectoryInfo(sessionPath).Name}", new Font("Arial", 12), Brushes.Gray, 50, 100);
            g.DrawString($"Scan Time: {DateTime.Now}", new Font("Arial", 12), Brushes.Black, 50, 130);
            g.DrawRectangle(Pens.Black, 40, 40, 720, 920);
            
            // Draw some dummy "content" lines
            for(int i=200; i<900; i+=30) 
                g.DrawLine(Pens.Black, 50, i, 750, i);

            bmp.Save(filePath, System.Drawing.Imaging.ImageFormat.Jpeg);
        }

        return filePath;
    }
}
