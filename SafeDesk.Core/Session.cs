namespace SafeDesk.Core;

public enum SessionState
{
    Inactive,
    Active,
    Ended
}

public enum SessionEndReason
{
    UserRequest,
    InactivityTimeout,
    AppExit,
    CrashRecovery
}

public class Session
{
    public Guid Id { get; private set; }
    public string FolderPath { get; private set; }
    public DateTime StartTime { get; private set; }
    public SessionState State { get; private set; }
    public List<SafeFile> CurrentFiles { get; private set; }
    
    public DateTime LastActivity { get; private set; }

    public Session(Guid id, string folderPath)
    {
        Id = id;
        FolderPath = folderPath;
        StartTime = DateTime.Now;
        LastActivity = DateTime.Now;
        State = SessionState.Active;
        CurrentFiles = new List<SafeFile>();
    }

    public void RefreshActivity()
    {
        LastActivity = DateTime.Now;
    }

    public void MarkEnded()
    {
        State = SessionState.Ended;
    }

    public void AddFile(SafeFile file)
    {
        CurrentFiles.Add(file);
        RefreshActivity();
    }
}
