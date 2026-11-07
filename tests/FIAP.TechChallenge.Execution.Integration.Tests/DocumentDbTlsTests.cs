using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FIAP.TechChallenge.Execution.Infrastructure.Persistence;
using MongoDB.Driver;

namespace FIAP.TechChallenge.Execution.Security.Tests;

public sealed class DocumentDbTlsTests
{
    private string certificatePath = null!;
    private RSA key = null!;
    private X509Certificate2 root = null!;
    private X509Certificate2 leaf = null!;

    [SetUp]
    public void SetUp()
    {
        key = RSA.Create(2048);
        var issuer = new CertificateRequest("CN=Test AWS CA", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        issuer.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        root = issuer.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
        var request = new CertificateRequest("CN=docdb.example.test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        leaf = request.Create(root, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(1), RandomNumberGenerator.GetBytes(16));
        certificatePath = Path.Combine(Path.GetTempPath(), "docdb-ca-" + Guid.NewGuid().ToString("N") + ".pem");
        File.WriteAllText(certificatePath, root.ExportCertificatePem());
    }

    [TearDown]
    public void TearDown() { File.Delete(certificatePath); leaf.Dispose(); root.Dispose(); key.Dispose(); }

    [Test]
    public void ValidatesPrivateTrustWithoutAcceptingHostMismatchOrUntrustedCertificates()
    {
        var settings = new MongoClientSettings();
        DocumentDbTls.Configure(settings, certificatePath);
        var callback = settings.SslSettings.ServerCertificateValidationCallback!;
        using var otherKey = RSA.Create(2048);
        var otherRequest = new CertificateRequest("CN=untrusted", otherKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var untrusted = otherRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(1));
        Assert.Multiple(() =>
        {
            Assert.That(settings.UseTls, Is.True);
            Assert.That(settings.AllowInsecureTls, Is.False);
            Assert.That(callback(this, leaf, null, SslPolicyErrors.RemoteCertificateChainErrors), Is.True);
            Assert.That(callback(this, leaf, null, SslPolicyErrors.RemoteCertificateNameMismatch), Is.False);
            Assert.That(callback(this, untrusted, null, SslPolicyErrors.RemoteCertificateChainErrors), Is.False);
            Assert.That(callback(this, null, null, SslPolicyErrors.RemoteCertificateNotAvailable), Is.False);
        });
    }

    [Test]
    public void RejectsEmptyCertificateBundle()
    {
        File.WriteAllText(certificatePath, string.Empty);

        Assert.That(() => DocumentDbTls.Configure(new MongoClientSettings(), certificatePath),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("vazio"));
    }

    [Test]
    public void RejectsMissingCertificateBundle()
    {
        File.Delete(certificatePath);

        Assert.That(() => DocumentDbTls.Configure(new MongoClientSettings(), certificatePath),
            Throws.TypeOf<FileNotFoundException>());
    }

    [Test]
    public void RejectsExpiredCertificates()
    {
        var request = new CertificateRequest("CN=docdb.example.test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var expired = request.Create(root, DateTimeOffset.UtcNow.AddHours(-3), DateTimeOffset.UtcNow.AddHours(-1), RandomNumberGenerator.GetBytes(16));
        var settings = new MongoClientSettings();
        DocumentDbTls.Configure(settings, certificatePath);
        Assert.That(settings.SslSettings.ServerCertificateValidationCallback!(this, expired, null,
            SslPolicyErrors.RemoteCertificateChainErrors), Is.False);
    }
}
