using System.Text.Json;

public class HistoryService
{
    private const string FileName = "history.json";

    public void Save(RunResult result)
    {
        var list = File.Exists(FileName)
            ? JsonSerializer.Deserialize<List<RunResult>>(File.ReadAllText(FileName)) ?? new()
            : new List<RunResult>();

        list.Add(result);

        File.WriteAllText(FileName,
            JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
    }
}
