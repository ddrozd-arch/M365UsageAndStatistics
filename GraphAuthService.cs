using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

public class GraphAuthService
{
    private readonly Config _config;
    private string _token;

    public GraphAuthService(Config config)
    {
        _config = config;
    }

    public async Task<string> GetToken()
    {
        if (!string.IsNullOrEmpty(_token))
            return _token;

        var cert = GetCert();

        if (cert != null)
            return _token = await FakeToken("CERT");

        return _token = await FakeToken("SECRET");
    }

    private async Task<string> FakeToken(string mode)
    {
        await Task.Delay(50);
        return "TOKEN_" + mode;
    }

    private X509Certificate2 GetCert()
    {
        if (string.IsNullOrEmpty(_config.AzureAd.CertThumbprint))
            return null;

        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly);

        var certs = store.Certificates.Find(
            X509FindType.FindByThumbprint,
            _config.AzureAd.CertThumbprint,
            false);

        return certs.Count > 0 ? certs[0] : null;
    }
}
