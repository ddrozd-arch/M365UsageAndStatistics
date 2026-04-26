using System.Text.Json;

public class Config
{
    public AzureAdConfig AzureAd { get; set; }
    public StorageConfig Storage { get; set; }
    public SecurityConfig Security { get; set; }

    public static Config Load()
    {
        return JsonSerializer.Deserialize<Config>(
            File.ReadAllText("appsettings.json"));
    }
}

public class AzureAdConfig
{
    public string TenantId { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string CertThumbprint { get; set; }
}

public class StorageConfig
{
    public string OutputDirectory { get; set; }
    public string FileNamePattern { get; set; }
}

public class SecurityConfig
{
    public string ApiKey { get; set; }
}