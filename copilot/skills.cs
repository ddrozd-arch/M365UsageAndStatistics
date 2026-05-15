using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class EnterprisePeopleSkillsServiceParallel
{
    private readonly GraphServiceClient _graph;
    private readonly string _outputDirectory;
    private readonly string _groupId;
    private readonly int _maxParallelism;

    public EnterprisePeopleSkillsServiceParallel(
        GraphServiceClient graphClient,
        string outputDirectory,
        string groupId = null,
        int maxParallelism = 8) // domyślnie 8 równoległych zapytań
    {
        _graph = graphClient;
        _outputDirectory = outputDirectory;
        _groupId = groupId;
        _maxParallelism = maxParallelism;
    }

    /// <summary>
    /// Pobiera skills dla wszystkich userów równolegle i zapisuje JSON + CSV.
    /// </summary>
    public async Task FetchAndSaveAllUsersAsync()
    {
        Directory.CreateDirectory(_outputDirectory);

        var users = await GetUsersAsync();

        Console.WriteLine($"[INFO] Pobieram skills dla {users.Count} użytkowników równolegle...");

        await Parallel.ForEachAsync(
            users,
            new ParallelOptions { MaxDegreeOfParallelism = _maxParallelism },
            async (user, ct) =>
            {
                try
                {
                    var json = await GetSkillsForUserAsync(user.Id);

                    var safeUpn = user.UserPrincipalName.Replace("@", "_").Replace(".", "_");
                    var jsonPath = Path.Combine(_outputDirectory, $"{safeUpn}_skills.json");
                    var csvPath = Path.Combine(_outputDirectory, $"{safeUpn}_skills.csv");

                    await File.WriteAllTextAsync(jsonPath, json, Encoding.UTF8, ct);

                    var csv = ConvertJsonToCsv(json);
                    await File.WriteAllTextAsync(csvPath, csv, Encoding.UTF8, ct);

                    Console.WriteLine($"[OK] {user.UserPrincipalName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] {user.UserPrincipalName}: {ex.Message}");
                }
            }
        );
    }

    /// <summary>
    /// Pobiera listę userów — z grupy jeśli podano groupId, inaczej cały tenant.
    /// </summary>
    private async Task<List<User>> GetUsersAsync()
    {
        var users = new List<User>();

        if (!string.IsNullOrEmpty(_groupId))
        {
            var members = await _graph.Groups[_groupId].Members
                .Request()
                .Select("id,displayName,userPrincipalName")
                .GetAsync();

            foreach (var m in members)
                if (m is User u)
                    users.Add(u);
        }
        else
        {
            var page = await _graph.Users
                .Request()
                .Select("id,displayName,userPrincipalName")
                .GetAsync();

            users.AddRange(page.CurrentPage);

            while (page.NextPageRequest != null)
            {
                page = await page.NextPageRequest.GetAsync();
                users.AddRange(page.CurrentPage);
            }
        }

        return users;
    }

    /// <summary>
    /// Pobiera People Skills dla jednego usera.
    /// </summary>
    private async Task<string> GetSkillsForUserAsync(string userId)
    {
        var requestUrl = $"{_graph.BaseUrl}/v1.0/users/{userId}/profile/skills";

        var request = new BaseRequest(requestUrl, _graph)
        {
            Method = "GET"
        };

        return await request.SendAsync<string>(null);
    }

    /// <summary>
    /// Spłaszcza People Skills do CSV.
    /// </summary>
    private string ConvertJsonToCsv(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var sb = new StringBuilder();

        sb.AppendLine("id,displayName,proficiency,categories,inferenceSource,inferenceConfidence");

        if (!doc.RootElement.TryGetProperty("value", out var arr))
            return sb.ToString();

        foreach (var item in arr.EnumerateArray())
        {
            var id = item.GetProperty("id").GetString();
            var name = item.GetProperty("displayName").GetString();
            var prof = item.TryGetProperty("proficiency", out var p) ? p.GetString() : "";

            var categories = "";
            if (item.TryGetProperty("categories", out var catArr))
                categories = string.Join(";", catArr.EnumerateArray().Select(c => c.GetString()));

            string inferenceSource = "";
            string inferenceConfidence = "";

            if (item.TryGetProperty("inference", out var inf))
            {
                inferenceSource = inf.TryGetProperty("source", out var s) ? s.GetString() : "";
                inferenceConfidence = inf.TryGetProperty("confidenceScore", out var c) ? c.GetDouble().ToString(CultureInfo.InvariantCulture) : "";
            }

            sb.AppendLine($"{id},{name},{prof},{categories},{inferenceSource},{inferenceConfidence}");
        }

        return sb.ToString();
    }
}


var graph = GraphClientFactory.Create();

var service = new EnterprisePeopleSkillsServiceParallel(
    graph,
    @"C:\CopilotSkills",
    "GUID-GRUPY",   // lub null → cały tenant
    maxParallelism: 10
);

await service.FetchAndSaveAllUsersAsync();
