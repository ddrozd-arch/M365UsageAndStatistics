public class OneDriveJob : IJob
{
    private readonly FilePathBuilder _path;
    private readonly HistoryService _history;

    public string Name => "onedrive";

    public OneDriveJob(FilePathBuilder path, HistoryService history)
    {
        _path = path;
        _history = history;
    }

    public async Task Run()
    {
        await Task.Delay(100);

        var file = _path.Build(Name);
        File.WriteAllText(file, "onedrive data");

        _history.Save(new RunResult
        {
            Date = DateTime.UtcNow,
            Job = Name,
            Status = "SUCCESS",
            Details = file
        });
    }
}
