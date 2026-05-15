using Microsoft.Graph;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class EnterpriseInteractionHistoryService
{
    private readonly GraphServiceClient _graph;
    private readonly string _outputDirectory;

    public EnterpriseInteractionHistoryService(GraphServiceClient graphClient, string outputDirectory)
    {
        _graph = graphClient;
        _outputDirectory = outputDirectory;
    }

    /// <summary>
    /// Pobiera interakcje Copilota ORG-WIDE i zapisuje jako JSON + CSV.
    /// Jeśli data == null → bierze dzień poprzedni.
    /// </summary>
    public async Task FetchAndSaveAsync(DateTime? date = null)
    {
        var json = await GetEnterpriseInteractionsAsync(date);

        var targetDate = date?.Date ?? DateTime.UtcNow.Date.AddDays(-1);
        var dateString = targetDate.ToString("yyyy-MM-dd");

        Directory.CreateDirectory(_outputDirectory);

        var jsonPath = Path.Combine(_outputDirectory, $"interactions_{dateString}.json");
        var csvPath  = Path.Combine(_outputDirectory, $"interactions_{dateString}.csv");

        await File.WriteAllTextAsync(jsonPath, json, Encoding.UTF8);

        var csv = ConvertJsonToCsv(json);
        await File.WriteAllTextAsync(csvPath, csv, Encoding.UTF8);
    }

    /// <summary>
    /// Pobiera interakcje ORG-WIDE z filtrem daty.
    /// </summary>
    public async Task<string> GetEnterpriseInteractionsAsync(DateTime? date = null)
    {
        var targetDate = date?.Date ?? DateTime.UtcNow.Date.AddDays(-1);

        var start = targetDate.ToString("yyyy-MM-ddT00:00:00Z", CultureInfo.InvariantCulture);
        var end   = targetDate.ToString("yyyy-MM-ddT23:59:59Z", CultureInfo.InvariantCulture);

        var filter = $"createdDateTime ge {start} and createdDateTime le {end}";

        var requestUrl =
            $"{_graph.BaseUrl}/beta/ai/interactionHistory/getAllEnterpriseInteractions?$filter={filter}";

        var request = new BaseRequest(requestUrl, _graph)
        {
            Method = "GET"
        };

        return await request.SendAsync<string>(null);
    }

    /// <summary>
    /// Spłaszcza JSON do CSV.
    /// </summary>
    private string ConvertJsonToCsv(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var sb = new StringBuilder();

        // Nagłówki CSV
        sb.AppendLine("id,userId,userPrincipalName,createdDateTime,interactionType,requestText,responseText,inputTokens,outputTokens,totalTokens");

        foreach (var item in doc.RootElement.GetProperty("value").EnumerateArray())
        {
            var id = item.GetProperty("id").GetString();
            var userId = item.GetProperty("userId").GetString();
            var upn = item.TryGetProperty("userPrincipalName", out var upnProp) ? upnProp.GetString() : "";
            var created = item.GetProperty("createdDateTime").GetString();
            var type = item.GetProperty("interactionType").GetString();

            var requestText = item.GetProperty("request").GetProperty("text").GetString().Replace("\"", "'");
            var responseText = item.GetProperty("response").GetProperty("text").GetString().Replace("\"", "'");

            var inputTokens = item.GetProperty("tokens").GetProperty("inputTokens").GetInt32();
            var outputTokens = item.GetProperty("tokens").GetProperty("outputTokens").GetInt32();
            var totalTokens = item.GetProperty("tokens").GetProperty("totalTokens").GetInt32();

            sb.AppendLine($"{id},{userId},{upn},{created},{type},\"{requestText}\",\"{responseText}\",{inputTokens},{outputTokens},{totalTokens}");
        }

        return sb.ToString();
    }
}


var graph = GraphClientFactory.Create(); // Twój klient Graph

var service = new EnterpriseInteractionHistoryService(
    graph,
    @"C:\CopilotData" // katalog wyjściowy
);

// Pobranie i zapis dla dnia poprzedniego
await service.FetchAndSaveAsync();

// Pobranie i zapis dla konkretnej daty
await service.FetchAndSaveAsync(new DateTime(2025, 11, 24));
