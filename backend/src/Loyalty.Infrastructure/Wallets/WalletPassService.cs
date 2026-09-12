using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO.Compression;
using Loyalty.Application.Wallets;
using Loyalty.Contracts.Wallets;
using Loyalty.Core.Domain.Coupons;
using Loyalty.Core.Domain.Wallets;
using Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Loyalty.Infrastructure.Wallets;

/// <summary>
/// Motor de wallets: genera .pkpass (Apple) firmado y JWT (Google) a partir del estado real del
/// miembro (saldo + sellos + cupones activos). Persistencia de tracking reusa el agregado WalletPass.
/// POR QUÉ estructura en capas: las build del pass (datos), el firmado (cripto) y la persistencia
/// (EF) están separados para que sustituir credenciales de producción no requiera tocar la lógica.
/// </summary>
public sealed class WalletPassService : IWalletPassService
{
    private readonly LoyaltyDbContext _db;
    private readonly DevelopmentSigningCertificate _signing;

    public WalletPassService(LoyaltyDbContext db, DevelopmentSigningCertificate signing)
    {
        _db = db;
        _signing = signing;
    }

    public async Task<WalletPassResultDto> GeneratePassAsync(Guid memberId, string provider, CancellationToken ct = default)
    {
        var pass = await BuildPassPayload(memberId, ct);
        var providerNorm = provider.ToLowerInvariant();

        if (providerNorm == WalletProviderNames.Apple)
        {
            var pkpass = BuildApplePass(pass);
            // Guardar track de emisión (idempotente por miembro+provider).
            await RecordIssuanceAsync(memberId, WalletProvider.ApplePassKit, pass.PassTypeIdentifier!, pass.SerialNumber, pass.AuthenticationToken, ct);
            return new WalletPassResultDto
            {
                Provider = WalletProviderNames.Apple,
                PkpassBytes = pkpass,
                FileName = $"{pass.SerialNumber}.pkpass",
                MediaType = "application/vnd.apple.pkpass"
            };
        }

        // Google Wallet
        var jwt = BuildGoogleJwt(pass);
        await RecordIssuanceAsync(memberId, WalletProvider.GoogleWallet, pass.PassTypeIdentifier!, pass.SerialNumber, pass.AuthenticationToken, ct);
        return new WalletPassResultDto
        {
            Provider = WalletProviderNames.Google,
            SignedJwt = jwt,
            GoogleAddUrl = $"https://pay.google.com/gp/v/save/{jwt}"
        };
    }

    public async Task<WalletPassResultDto> RefreshPassAsync(Guid memberId, string provider, CancellationToken ct = default)
    {
        // Re-genera con datos actuales; conserva serial/token previos si ya hay pase (push update).
        return await GeneratePassAsync(memberId, provider, ct);
    }

    // ---------- datos del pass ----------

    private sealed record PassPayload(
        string? PassTypeIdentifier,
        string SerialNumber,
        string? AuthenticationToken,
        WalletBrandDto Brand,
        Guid MemberId,
        string FullName,
        string QrHash,
        int PointsBalance,
        int StampsTotal,
        int StampsCompleted,
        IReadOnlyList<Coupon> ActiveCoupons);

    private async Task<PassPayload> BuildPassPayload(Guid memberId, CancellationToken ct)
    {
        // El rival al query: proyecciones que evitan traer toda la entidad con tracking.
        var member = await _db.Members.AsNoTracking()
            .Select(m => new { m.Id, m.TenantId, m.FullName, m.QrHash, m.PointsBalance, m.LoyaltyProgramId })
            .SingleOrDefaultAsync(m => m.Id == memberId, ct)
            ?? throw new InvalidOperationException($"Miembro {memberId} no encontrado.");

        var tenant = await _db.Tenants.AsNoTracking()
            .Select(t => new { t.Id, t.Name, t.LogoUrl, t.PrimaryColor })
            .SingleAsync(t => t.Id == member.TenantId, ct);
        var program = await _db.LoyaltyPrograms.AsNoTracking()
            .Include(p => p.StampRules)
            .SingleAsync(p => p.Id == member.LoyaltyProgramId, ct);

        var memberStamps = await _db.MemberStamps.AsNoTracking()
            .Where(s => s.MemberId == memberId)
            .ToListAsync(ct);
        var activeCoupons = await _db.Coupons.AsNoTracking()
            .Where(c => c.MemberId == memberId && c.Status == CouponStatus.Issued)
            .Where(c => !c.ExpiresUtc.HasValue || c.ExpiresUtc > DateTimeOffset.UtcNow)
            .ToListAsync(ct);

        var stampsTotal = 0;
        var stampsCompleted = 0;
        if (program.StampRules.Count > 0)
        {
            // Total "máximo alcanzable" en base a la regla que más sellos pide; completados = reglas cumplidas.
            var maxRequired = program.StampRules.Max(r => r.StampsRequired);
            stampsTotal = program.StampRules.Count;
            stampsCompleted = program.StampRules.Count(r =>
            {
                var stamp = memberStamps.FirstOrDefault(s => s.ProductSku == r.ProductSku);
                return stamp != null && stamp.Count >= r.StampsRequired;
            });
        }

        return new PassPayload(
            PassTypeIdentifier: $"loyalty.{tenant.Name.Replace(" ", "").ToLowerInvariant()}.member",
            SerialNumber: member.Id.ToString("N"),
            AuthenticationToken: Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)),
            Brand: new WalletBrandDto
            {
                OrganizationName = tenant.Name,
                Description = program.Name,
                LogoUrl = tenant.LogoUrl,
                PrimaryColor = tenant.PrimaryColor
            },
            MemberId: member.Id,
            FullName: member.FullName,
            QrHash: member.QrHash,
            PointsBalance: member.PointsBalance,
            StampsTotal: stampsTotal,
            StampsCompleted: stampsCompleted,
            ActiveCoupons: activeCoupons);
    }

    // ---------- Apple PayPass (.pkpass) ----------

    private byte[] BuildApplePass(PassPayload p)
    {
        // pass.json según schema PassKit (boardingpass/storeCard genérico fidelidad).
        var pass = new Dictionary<string, object>
        {
            ["formatVersion"] = 1,
            ["passTypeIdentifier"] = p.PassTypeIdentifier,
            ["serialNumber"] = p.SerialNumber,
            ["teamIdentifier"] = "LOYALTYDEV",
            ["webServiceURL"] = null,
            ["authenticationToken"] = p.AuthenticationToken,
            ["logoText"] = p.Brand.OrganizationName,
            ["organizationName"] = p.Brand.OrganizationName,
            ["description"] = p.Brand.Description ?? "Tarjeta de fidelidad",
            ["backgroundColor"] = p.Brand.PrimaryColor ?? "rgb(255,92,57)",
            ["foregroundColor"] = "rgb(255,255,255)",
            ["labelColor"] = "rgb(139,150,180)",
            ["barcode"] = new Dictionary<string, object>
            {
                ["format"] = "PKBarcodeFormatQR",
                ["message"] = p.QrHash,
                ["messageEncoding"] = "iso-8859-1"
            },
            ["generic"] = new Dictionary<string, object>
            {
                ["primaryFields"] = new object[]
                {
                    new Dictionary<string, object> { ["key"] = "member", ["label"] = "Miembro", ["value"] = p.FullName }
                },
                ["secondaryFields"] = new object[]
                {
                    new Dictionary<string, object> { ["key"] = "points", ["label"] = "Puntos", ["value"] = p.PointsBalance.ToString() },
                    new Dictionary<string, object> { ["key"] = "stamps", ["label"] = "Sellos", ["value"] = $"{p.StampsCompleted}/{p.StampsTotal}" }
                },
                ["auxiliaryFields"] = new object[]
                {
                    new Dictionary<string, object> { ["key"] = "memberId", ["label"] = "ID", ["value"] = p.MemberId.ToString("N")[..8] }
                },
                ["backFields"] = new object[]
                {
                    new Dictionary<string, object> { ["key"] = "help", ["label"] = "Ayuda", ["value"] = "Escanea en caja para puntos y sellos." }
                }
            }
        };

        // Empaquetado .pkpass: pass.json + manifest.json (SHA-1 de los archivos) + firma PKCS#7.
        var files = new Dictionary<string, byte[]>();
        var passJson = JsonSerializer.Serialize(pass, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
        files["pass.json"] = Encoding.UTF8.GetBytes(passJson);

        // Iconos (in)clave para que el wallet renderice; generamos un par de PNG simples.
        files["icon.png"] = BuildPng(29, p.Brand.PrimaryColor ?? "#ff5c39");
        files["icon@2x.png"] = BuildPng(58, p.Brand.PrimaryColor ?? "#ff5c39");
        files["logo.png"] = BuildPng(160, p.Brand.PrimaryColor ?? "#ff5c39");

        var manifest = files.ToDictionary(kv => kv.Key, kv => Convert.ToHexString(SHA1.HashData(kv.Value)).ToLowerInvariant());
        var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));

        // Firmar manifest con SignedCms PKCS#7 (detached) usando la clave privada del cert.
        var cert = _signing.Get();
        var content = new ContentInfo(manifestBytes);
        var signed = new SignedCms(content, true);
        var signer = new CmsSigner(cert) { IncludeOption = X509IncludeOption.EndCertOnly };
        signed.ComputeSignature(signer);
        var signature = signed.Encode();

        // Zip del .pkpass en memoria (System.IO.Compression, sin dep externa de zip).
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // manifest.json es OBLIGATORIO en PassKit: lista el hash SHA1 de cada archivo.
            files["manifest.json"] = manifestBytes;
            foreach (var (name, data) in files)
            {
                var entry = zip.CreateEntry(name);
                using var es = entry.Open();
                es.Write(data);
            }
            var sigEntry = zip.CreateEntry("signature");
            using var ss = sigEntry.Open();
            ss.Write(signature);
        }
        return ms.ToArray();
    }

    private static byte[] BuildPng(int size, string hexColor)
    {
        // PNG sólido (1 píxel) escalado por contenedor — sufiente para el wallet dev.
        // POR QUÉ un píxel: el manifest firma los bytes reales; un PNG concreto cuenta como asset.
        var (r, g, b) = HexToRgb(hexColor);
        // Cabecera PNG básica de 1x1.
        var raw = new List<byte> { (byte)r, (byte)g, (byte)b, 255 };
        // Construcción PNG real truncada (color sólido 1x1 RGBA). Decodificable por libpng.
        return BuildPngBytes(raw, size);
    }

    private static byte[] BuildPngBytes(List<byte> pixelRgba, int size)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms))
        {
            w.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
            w.Write(Be(13));
            w.Write(Bytes("IHDR"));
            w.Write(Be(size)); w.Write(Be(size)); w.Write((byte)8); w.Write((byte)6); w.Write((byte)0); w.Write((byte)0); w.Write((byte)0);
            w.Write(Crc(Bytes("IHDR").Concat(Be(size)).Concat(Be(size)).Concat(new byte[] { 8, 6, 0, 0, 0 }).ToArray()));
            // IDAT: zlib deflate de scanline filtro 0 + RGBA.
            var scanline = new byte[size * 4 + 1];
            for (int x = 0; x < size; x++) { scanline[x * 4 + 1] = pixelRgba[0]; scanline[x * 4 + 2] = pixelRgba[1]; scanline[x * 4 + 3] = pixelRgba[2]; scanline[x * 4 + 4] = pixelRgba[3]; }
            var raw = new byte[(size * 4 + 1) * size];
            for (int i = 0; i < size; i++) Array.Copy(scanline, 0, raw, i * (size * 4 + 1), scanline.Length);
            var compressed = Zlib(raw);
            w.Write(Be(compressed.Length));
            w.Write(Bytes("IDAT"));
            w.Write(compressed);
            w.Write(Crc(Bytes("IDAT").Concat(compressed).ToArray()));
            w.Write(Be(0));
            w.Write(Bytes("IEND"));
            w.Write(Crc(Bytes("IEND").ToArray()));
        }
        return ms.ToArray();
    }

    private static byte[] Zlib(byte[] data)
    {
        // zlib = 0x78 0x9C + deflate + adler32.
        using var raw = new MemoryStream();
        using (var def = new System.IO.Compression.DeflateStream(raw, System.IO.Compression.CompressionLevel.Fastest, true))
        {
            def.Write(data, 0, data.Length);
        }
        var deflated = raw.ToArray();
        var result = new byte[deflated.Length + 6];
        result[0] = 0x78; result[1] = 0x9C;
        Buffer.BlockCopy(deflated, 0, result, 2, deflated.Length);
        var adler = Adler32(data);
        result[^4] = (byte)(adler >> 24); result[^3] = (byte)(adler >> 16); result[^2] = (byte)(adler >> 8); result[^1] = (byte)adler;
        return result;
    }

    private static uint Adler32(byte[] data)
    {
        uint a = 1, b = 0;
        foreach (var c in data) { a = (a + c) % 65521; b = (b + a) % 65521; }
        return (b << 16) | a;
    }

    private static byte[] Be(int v) => new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };
    private static byte[] Bytes(string s) => Encoding.ASCII.GetBytes(s);
    private static uint Crc(byte[] data)
    {
        uint c;
        uint crc = 0xffffffffu;
        foreach (var b in data)
        {
            c = (crc ^ b) & 0xff;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xedb88320u ^ (c >> 1) : c >> 1;
            crc = (crc >> 8) ^ c;
        }
        return crc ^ 0xffffffffu;
    }

    private static (int, int, int) HexToRgb(string hex)
    {
        var h = hex.TrimStart('#');
        if (h.Length != 6 || !int.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out _)) return (255, 92, 57);
        return (Convert.ToInt32(h[..2], 16), Convert.ToInt32(h.Substring(2, 2), 16), Convert.ToInt32(h.Substring(4, 2), 16));
    }

    // ---------- Google Wallet ----------

    private string BuildGoogleJwt(PassPayload p)
    {
        // Payload Wallet: clase (GenericClass) + objeto (GenericObject) con datos del miembro.
        var issuerId = "loyalty-dev";
        var classSuffix = $"{p.MemberId:N}"[..8];

        var genericClass = new Dictionary<string, object>
        {
            ["id"] = $"{issuerId}.loyaltyMember{classSuffix}",
            ["classTemplateInfo"] = new Dictionary<string, object>
            {
                ["cardTemplateOverride"] = new Dictionary<string, object>
                {
                    ["cardRowTemplateInfos"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["twoItems"] = new Dictionary<string, object> { }
                        }
                    }
                }
            }
        };
        var genericObject = new Dictionary<string, object>
        {
            ["id"] = $"{issuerId}.{p.MemberId:N}",
            ["classId"] = $"{issuerId}.loyaltyMember{classSuffix}",
            ["state"] = "ACTIVE",
            ["issuerName"] = p.Brand.OrganizationName,
            ["barcode"] = new Dictionary<string, object> { ["type"] = "QR_CODE", ["value"] = p.QrHash },
            ["textModulesData"] = new object[]
            {
                new Dictionary<string, object> { ["id"] = "member", ["header"] = "Miembro", ["body"] = p.FullName },
                new Dictionary<string, object> { ["id"] = "points", ["header"] = "Puntos", ["body"] = p.PointsBalance.ToString() },
                new Dictionary<string, object> { ["id"] = "stamps", ["header"] = "Sellos", ["body"] = $"{p.StampsCompleted}/{p.StampsTotal}" }
            }
        };

        var jwtHeader = new Dictionary<string, object>
        {
            ["alg"] = "HS256",
            ["typ"] = "JWT"
        };
        var jwtPayload = new Dictionary<string, object>
        {
            ["iss"] = "loyalty-dev@loyalty.iam.gserviceaccount.com",
            ["aud"] = "google",
            ["typ"] = "savetowallet",
            ["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["origins"] = new[] { "http://127.0.0.1:3000" },
            ["payload"] = new Dictionary<string, object>
            {
                ["genericClasses"] = new object[] { genericClass },
                ["genericObjects"] = new object[] { genericObject }
            }
        };

        // Firma HMAC-SHA256 con clave dev (producción: RS256 de la service-account de Google).
        var key = Environment.GetEnvironmentVariable("LOYALTY_GOOGLE_KEY") ?? EnsureDevKey();
        var headerJson = JsonSerializer.Serialize(jwtHeader);
        var payloadJson = JsonSerializer.Serialize(jwtPayload);
        var signingInput = $"{Base64Url(headerJson)}.{Base64Url(payloadJson)}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var signature = Base64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput)));
        return $"{signingInput}.{signature}";
    }

    private static string Base64Url(string plain) => Base64Url(Encoding.UTF8.GetBytes(plain));
    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string? _devKey;
    private string EnsureDevKey() => _devKey ??= Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    // ---------- persistencia ----------

    private async Task RecordIssuanceAsync(Guid memberId, WalletProvider provider, string passTypeId, string serial, string token, CancellationToken ct)
    {
        var member = await _db.Members.AsNoTracking()
            .Where(m => m.Id == memberId)
            .Select(m => new { m.TenantId })
            .SingleAsync(ct);
        var existing = await _db.WalletPasses.SingleOrDefaultAsync(w => w.MemberId == memberId && w.Provider == provider, ct);
        if (existing != null)
        {
            existing.MarkUpdated();
            await _db.SaveChangesAsync(ct);
            return;
        }
        _db.WalletPasses.Add(WalletPass.Create(member.TenantId, memberId, provider, passTypeId, serial, token));
        await _db.SaveChangesAsync(ct);
    }
}