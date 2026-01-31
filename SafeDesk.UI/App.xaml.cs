using System.Windows;
using SafeDesk.Core;

namespace SafeDesk.UI;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try 
        {
            // Initialize the Core system (Create folders, etc.)
            CoreInitializer.InitializeSystem();
        } 
        catch (System.Exception ex) 
        {
            MessageBox.Show($"Critical Error Initializing SafeDesk: {ex.Message}", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }
}
