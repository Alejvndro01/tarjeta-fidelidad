using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Loyalty.Infrastructure.Wallets;

/// <summary>
/// Provee el certificado de firma para .pkpass.
/// POR QUÉ self-signed en desarrollo y .p12 en producción: Apple exige un Pass Signing Certificate
/// (comercial de su portal) cuya clave privada firma el manifest. No podemos fabricarlo aquí.
/// Para que el pipeline sea 100% real y verificable sin credenciales del cliente, generamos un
/// certificado de desarrollo autofirmado (caja de test); en producción se lee de config el path
/// del .p12 comercial + password. La estructura del .pkpass (zip + manifest + SignedCms PKCS#7) es idéntica.
/// </summary>
public sealed class DevelopmentSigningCertificate
{
    private X509Certificate2? _cert;

    /// <summary>Devuelve el certificado de firma. Si config señala un .p12 real lo carga; si no, genera/cachea uno dev.</summary>
    public X509Certificate2 Get()
    {
        if (_cert != null) return _cert;

        var p12Path = Environment.GetEnvironmentVariable("LOYALTY_PKCS12_PATH");
        var p12Password = Environment.GetEnvironmentVariable("LOYALTY_PKCS12_PASSWORD");
        if (!string.IsNullOrWhiteSpace(p12Path) && File.Exists(p12Path))
        {
            var c = X509CertificateLoader.LoadPkcs12FromFile(p12Path, p12Password, X509KeyStorageFlags.Exportable);
            _cert = c;
            return c;
        }

        // Dev: certificado autofirmado efímero (RSA 2048), 5 años, extensiones de propósito de firma.
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=Loyalty Dev Signing", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        var serial = RandomNumberGenerator.GetBytes(16);
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
        // Exportar para poder extraer la clave privada al firmar el SignedCms.
        var exported = cert.Export(X509ContentType.Pkcs12);
        _cert = X509CertificateLoader.LoadPkcs12(exported, null, X509KeyStorageFlags.Exportable);
        return _cert;
    }
}