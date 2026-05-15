using Microsoft.Graph;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

public class EnterpriseMessageTraceService
{
    private readonly GraphServiceClient _graph;
    private readonly string _outputDirectory;

    public EnterpriseMessageTraceService(GraphServiceClient graphClient, string outputDirectory)
    {
        _graph = graphClient;
        _outputDirectory = outputDirectory;
    }

    /// <summary>
    /// Pobiera Message Trace ORG-WIDE z filtrowaniem po dacie (00:00–23:59:59).
    /// Jeśli data == null → poprzedni dzień.
    /// Zapisuje JSON + CSV.
    /// </summary>
    public async Task FetchAndSaveAsync(DateTime? date = null)
    {
        var json = await GetMessageTraceAsync(date);

        var targetDate = date?.Date ?? DateTime.UtcNow.Date.AddDays(-1);
        var dateString = targetDate.ToString("yyyy-MM-dd");

        Directory.CreateDirectory(_outputDirectory);

        var jsonPath = Path.Combine(_outputDirectory, $"messageTrace_{dateString}.json");
        var csvPath  = Path.Combine(_outputDirectory, $"messageTrace_{dateString}.csv");

        await File.WriteAllTextAsync(jsonPath, json, Encoding.UTF8);

        var csv = ConvertJsonToCsv(json);
        await File.WriteAllTextAsync(csvPath, csv, Encoding.UTF8);
    }

    /// <summary>
    /// Pobiera Message Trace ORG-WIDE z paginacją.
    /// </summary>
    public async Task<string> GetMessageTraceAsync(DateTime? date = null)
    {
        var targetDate = date?.Date ?? DateTime.UtcNow.Date.AddDays(-1);

        var start = targetDate.ToString("yyyy-MM-ddT00:00:00Z", CultureInfo.InvariantCulture);
        var end   = targetDate.ToString("yyyy-MM-ddT23:59:59Z", CultureInfo.InvariantCulture);

        var filter = $"receivedDateTime ge {start} and receivedDateTime le {end}";

        var requestUrl =
            $"{_graph.BaseUrl}/beta/admin/exchange/tracing/messageTraces?$filter={filter}";

        var request = new BaseRequest(requestUrl, _graph)
        {
            Method = "GET"
        };

        var allItems = new List<JsonElement>();

        // pobranie pierwszej strony
        var response = await request.SendAsync<JsonDocument>(null);
        AddPage(response, allItems);

        // paginacja
        while (response.RootElement.TryGetProperty("@odata.nextLink", out var nextLinkProp))
        {
            var nextUrl = nextLinkProp.GetString();
            var nextReq = new BaseRequest(nextUrl, _graph) { Method = "GET" };
            response = await nextReq.SendAsync<JsonDocument>(null);
            AddPage(response, allItems);
        }

        // składamy finalny JSON
        var finalJson = JsonSerializer.Serialize(new { value = allItems }, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        return finalJson;
    }

    private void AddPage(JsonDocument doc, List<JsonElement> list)
    {
        if (doc.RootElement.TryGetProperty("value", out var arr))
        {
            foreach (var item in arr.EnumerateArray())
                list.Add(item.Clone());
        }
    }

    /// <summary>
    /// Spłaszcza Message Trace do CSV.
    /// </summary>
    private string ConvertJsonToCsv(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var sb = new StringBuilder();

        sb.AppendLine("id,messageId,status,receivedDateTime,senderAddress,recipientAddress,subject,size,fromIP,toIP");

        if (!doc.RootElement.TryGetProperty("value", out var arr))
            return sb.ToString();

        foreach (var item in arr.EnumerateArray())
        {
            string id = item.GetProperty("id").GetString();
            string messageId = item.GetProperty("messageId").GetString().Replace("\"", "'");
            string status = item.GetProperty("status").GetString();
            string received = item.GetProperty("receivedDateTime").GetString();
            string sender = item.GetProperty("senderAddress").GetString();
            string recipient = item.GetProperty("recipientAddress").GetString();
            string subject = item.GetProperty("subject").GetString().Replace("\"", "'");
            int size = item.GetProperty("size").GetInt32();
            string fromIP = item.GetProperty("fromIP").GetString();
            string toIP = item.GetProperty("toIP").GetString();

            sb.AppendLine($"{id},\"{messageId}\",{status},{received},{sender},{recipient},\"{subject}\",{size},{fromIP},{toIP}");
        }

        return sb.ToString();
    }
}
