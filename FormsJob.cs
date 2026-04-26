public class FormsJob : IJob
{
    private readonly FormsService _forms;
    private readonly FilePathBuilder _path;
    private readonly HistoryService _history;

    public string Name => "forms";

    public FormsJob(FormsService forms, FilePathBuilder path, HistoryService history)
    {
        _forms = forms;
        _path = path;
        _history = history;
    }

    public async Task Run()
    {
        var data = await _forms.GetFormsUsage();
        var file = _path.Build(Name);

        File.WriteAllText(file, data);

        _history.Save(new RunResult
        {
            Date = DateTime.UtcNow,
            Job = Name,
            Status = "SUCCESS",
            Details = file
        });
    }
}
