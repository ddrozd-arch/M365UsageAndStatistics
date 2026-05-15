using GraphSdk;

var token = "TOKEN";

var httpClient = new HttpClient(
    new HttpClientHandler
    {
        AllowAutoRedirect = true
    });

var graph = new GraphClient(
    httpClient,
    token,
    new GraphClientOptions
    {
        MaxRetries = 5,
        DelayBetweenRequestsMs = 500
    });

var users =
    await graph.GetPagedAsync<GraphUser>(
        "https://graph.microsoft.com/v1.0/users");

Console.WriteLine(users.Count);

using var response =
    await graph.GetAsync(
        "https://graph.microsoft.com/beta/reports/getFormsUserActivityUserCounts(period='D7')?$format=text/csv");

await graph.SaveToFileAsync(
    response,
    @"C:\Reports\forms.csv");

using var response =
    await graph.GetAsync(
        "https://graph.microsoft.com/v1.0/me/photo/$value");

await graph.SaveToFileAsync(
    response,
    @"C:\Temp\photo.jpg");