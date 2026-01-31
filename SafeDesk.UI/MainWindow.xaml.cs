using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using SafeDesk.Core;

namespace SafeDesk.UI;

public partial class MainWindow : Window
{
    private readonly SessionManager _sessionManager;
    private DispatcherTimer _timer;
    private DateTime _sessionStartTime;
    private ObservableCollection<SafeFile> _files;

    public MainWindow()
    {
        InitializeComponent();
        _sessionManager = new SessionManager();
        _files = new ObservableCollection<SafeFile>();
        FileList.ItemsSource = _files;

        // Init Timer
        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += Timer_Tick;

        UpdateUIState(false);
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        var elapsed = DateTime.Now - _sessionStartTime;
        TxtTimer.Text = elapsed.ToString(@"mm\:ss");
    }

    private void BtnStartSession_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var session = _sessionManager.StartNewSession();
            
            _sessionStartTime = session.StartTime;
            _timer.Start();

            TxtSessionId.Text = $"ID: {session.Id}";
            StatusMessage.Content = $"Session Active at {session.FolderPath}";
            
            UpdateUIState(true);
            RefreshFileList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error starting session: {ex.Message}", "Full Security Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnEndSession_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Are you sure you want to end the session? The workspace will be locked.", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            _sessionManager.EndSession();
            _timer.Stop();
            UpdateUIState(false);
            _files.Clear();
            StatusMessage.Content = "Session Ended";
            TxtTimer.Text = "00:00";
            TxtSessionId.Text = "ID: -";
        }
    }

    private void BtnImportFile_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Secure Import - Select File",
            Multiselect = true, // Allow multiple files
            CheckFileExists = true
        };

        if (openFileDialog.ShowDialog() == true)
        {
            ImportFiles(openFileDialog.FileNames);
        }
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (_sessionManager.CurrentSession == null || !_sessionManager.CurrentSession.IsActive)
            return;

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            ImportFiles(files);
        }
    }

    private void ImportFiles(string[] filePaths)
    {
        int successCount = 0;
        foreach (var path in filePaths)
        {
            try
            {
                // UI STRICTLY delegates to Core
                _sessionManager.ImportFile(path);
                successCount++;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import {System.IO.Path.GetFileName(path)}: {ex.Message}", "Security Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        if (successCount > 0)
        {
            RefreshFileList();
        }
    }

    private void RefreshFileList()
    {
        _files.Clear();
        foreach (var f in _sessionManager.GetSessionFiles())
        {
            _files.Add(f);
        }
    }

    private void UpdateUIState(bool isSessionActive)
    {
        // Buttons
        BtnStartSession.IsEnabled = !isSessionActive;
        BtnEndSession.IsEnabled = isSessionActive;
        BtnImportFile.IsEnabled = isSessionActive;

        // Visuals
        StatusIndicator.Background = isSessionActive ? new SolidColorBrush(Color.FromRgb(76, 199, 30)) : new SolidColorBrush(Color.FromRgb(80, 80, 80));
        StatusText.Text = isSessionActive ? "System Active - Secured" : "Idle";
        StatusText.Foreground = isSessionActive ? new SolidColorBrush(Colors.LightGreen) : new SolidColorBrush(Colors.Gray);
        
        TxtTimer.Foreground = isSessionActive ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Color.FromRgb(60,60,60));

        // Drag Drop Overlay
        EmptyStateText.Visibility = isSessionActive ? Visibility.Collapsed : Visibility.Visible;
        if (isSessionActive && _files.Count == 0) EmptyStateText.Text = "Drag files here or click Import";
        else if (!isSessionActive) EmptyStateText.Text = "Start a session to use the workspace";
    }
}
