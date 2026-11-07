using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using MongoDB.Driver;

namespace FIAP.TechChallenge.Execution.Infrastructure.Persistence;

public static class MongoTls
{
    public static void Configure(MongoClientSettings settings, string certificatePath)
    {
        var roots = new X509Certificate2Collection();
        roots.ImportFromPemFile(certificatePath);
        if (roots.Count == 0) throw new InvalidOperationException("O bundle de CAs MongoDB está vazio.");
        settings.UseTls = true;
        settings.AllowInsecureTls = false;
        settings.SslSettings = new SslSettings
        {
            ServerCertificateValidationCallback = (_, certificate, _, errors) =>
            {
                if (certificate is null || (errors & (SslPolicyErrors.RemoteCertificateNameMismatch |
                    SslPolicyErrors.RemoteCertificateNotAvailable)) != 0) return false;
                using var chain = new X509Chain();
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.AddRange(roots);
                chain.ChainPolicy.ExtraStore.AddRange(roots);
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                using var leaf = X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
                return chain.Build(leaf);
            }
        };
    }
}
