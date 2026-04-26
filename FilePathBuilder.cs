public class FilePathBuilder
{
    private readonly Config _config;

    public FilePathBuilder(Config config)
    {
        _config = config;
    }

    public string Build(string job)
    {
        var dir = _config.Storage.OutputDirectory;

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var file = _config.Storage.FileNamePattern
            .Replace("{job}", job)
            .Replace("{date}", date);

        return Path.Combine(dir, file);
    }
}