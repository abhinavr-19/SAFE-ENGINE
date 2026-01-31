using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SafeDesk.Core;

namespace SafeDesk.UI;

public partial class MainWindow : Window
{
    private readonly SessionManager _sessionManager;
    private ObservableCollection<SafeFile> _files;

    public MainWindow()
    {
        InitializeComponent();
        
        // 1. Initialize Core
        _sessionManager = new SessionManager();
        
        // 2. Setup Data Binding
        _files = new ObservableCollection<SafeFile>();
        FileList.ItemsSource = _files;

        // 3. Subscribe to Core Events
        _sessionManager.StateChanged += OnSessionStateChanged;
        _sessionManager.TimeRemainingChanged += OnTimeRemainingChanged;
        _sessionManager.LogUpdated += OnLogUpdated;

        // 4. Initial State
        RefreshLogs();
        UpdateUIState(SessionState.Inactive);
    }

    // --- Event Handlers (Cross-Thread Marshaling) ---

    private void OnSessionStateChanged(SessionState newState)
    {
        Dispatcher.Invoke(() => UpdateUIState(newState));
    }

    private void OnTimeRemainingChanged(TimeSpan remaining)
    {
        Dispatcher.Invoke(() => 
        {
            TxtInactivityTimer.Text = remaining.ToString(@"mm\:ss");
            if (remaining.TotalMinutes < 1) 
            {
                TxtInactivityTimer.Foreground = new SolidColorBrush(Colors.Red);
            }
            else
            {
                TxtInactivityTimer.Foreground = new SolidColorBrush(Colors.White);
            }
        });
    }

    private void OnLogUpdated(string logs)
    {
        Dispatcher.Invoke(() => TxtAuditLogs.Text = logs);
    }


    // --- UI Interactions ---

    private void BtnStartSession_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var session = _sessionManager.StartNewSession();
            TxtSessionId.Text = $"ID: {session.Id}";
            StatusMessage.Content = $"Session Active at {session.FolderPath}";
            // State update handled by event
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error starting session: {ex.Message}", "Security Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnEndSession_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Are you sure you want to end the session? All files will be wiped.", "Confirm End Session", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            _sessionManager.EndSession(SessionEndReason.UserRequest);
        }
    }

    private void BtnImportFile_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Secure Import - Select File",
            Multiselect = true,
            CheckFileExists = true
        };

        if (openFileDialog.ShowDialog() == true)
        {
            ImportFiles(openFileDialog.FileNames);
        }
    }

    private void BtnScan_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusMessage.Content = "Scanning document...";
            var file = _sessionManager.ScanDocument();
            RefreshFileList();
            MessageBox.Show($"Scanned document received: {file.FileName}", "Scan Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            StatusMessage.Content = "Ready";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Scan Error: {ex.Message}", "Device Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        if (FileList.SelectedItem is not SafeFile file)
        {
            MessageBox.Show("Please select a file from the list to print.", "Print Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            StatusMessage.Content = $"Printing {file.FileName}...";
            _sessionManager.PrintFile(file);
            MessageBox.Show("File sent to the secure printer job queue.", "Print Success", MessageBoxButton.OK, MessageBoxImage.Information);
            StatusMessage.Content = "Ready";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Print Error: {ex.Message}", "Device Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (_sessionManager == null || _sessionManager.CurrentSession == null || _sessionManager.CurrentSession.State != SessionState.Active)
            return;

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            ImportFiles(files);
        }
    }

    // Inactivity Detection Hooks
    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        _sessionManager.NotifyUserInteraction();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        _sessionManager.NotifyUserInteraction();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        // Enforce cleanup on app exit
        if (_sessionManager.CurrentSession != null && _sessionManager.CurrentSession.State == SessionState.Active)
        {
             _sessionManager.EndSession(SessionEndReason.AppExit);
        }
    }


    // --- Helper Logic ---

    private void ImportFiles(string[] filePaths)
    {
        foreach (var path in filePaths)
        {
            try
            {
                _sessionManager.ImportFile(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import {System.IO.Path.GetFileName(path)}: {ex.Message}", "Security Error");
            }
        }
        RefreshFileList();
    }

    private void RefreshFileList()
    {
        _files.Clear();
        foreach (var f in _sessionManager.GetSessionFiles())
        {
            _files.Add(f);
        }
    }

    private void RefreshLogs()
    {
        TxtAuditLogs.Text = _sessionManager.GetAuditLogs();
        TxtAuditLogs.ScrollToEnd();
    }

    private void UpdateUIState(SessionState state)
    {
        bool isActive = state == SessionState.Active;

        // Buttons
        BtnStartSession.IsEnabled = !isActive;
        BtnEndSession.IsEnabled = isActive;
        BtnImportFile.IsEnabled = isActive;
        BtnPrint.IsEnabled = isActive;
        BtnScan.IsEnabled = isActive;

        if (isActive)
        {
            // Active
            StatusIndicator.Background = new SolidColorBrush(Color.FromRgb(76, 199, 30)); // Green
            StatusText.Text = "System Active - Secured";
            StatusText.Foreground = new SolidColorBrush(Colors.LightGreen);
            
            EmptyStateText.Visibility = Visibility.Collapsed;
            if (_files.Count == 0) EmptyStateText.Visibility = Visibility.Visible;
            if (_files.Count == 0) EmptyStateText.Text = "Drag files here or click Import";
        }
        else
        {
            // Inactive / Ended
            StatusIndicator.Background = new SolidColorBrush(Color.FromRgb(80, 80, 80)); // Gray
            StatusText.Text = state == SessionState.Ended ? "Session Ended - Files Wiped" : "Idle";
            StatusText.Foreground = new SolidColorBrush(Colors.Gray);
            
            TxtInactivityTimer.Text = "00:00";
            TxtInactivityTimer.Foreground = new SolidColorBrush(Color.FromRgb(60,60,60));
            TxtSessionId.Text = "ID: -";
            StatusMessage.Content = "Ready";

            _files.Clear();
            EmptyStateText.Visibility = Visibility.Visible;
            EmptyStateText.Text = "Start a session to use the workspace";
        }

        RefreshLogs();
    }
}
