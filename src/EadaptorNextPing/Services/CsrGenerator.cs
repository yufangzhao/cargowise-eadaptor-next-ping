using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace EadaptorNextPing.Services;

public sealed record CsrResult(string CsrPem, string KeyPem, string KeyAlgorithm);

public static class CsrGenerator
{
    public static CsrResult Generate(string commonName, string organization, string organizationalUnit,
        string country, int keySize, string? keyPassword)
    {
        if (string.IsNullOrWhiteSpace(commonName))
            throw new ArgumentException("Common Name (CN) is required.");
        country = country?.Trim() ?? string.Empty;
        if (country.Length > 0 && country.Length != 2)
            throw new ArgumentException("Country code (C) must be exactly 2 letters, e.g. CN, GB, US.");
        if (keySize != 2048 && keySize != 3072 && keySize != 4096)
            keySize = 2048;

        var subject = BuildSubject(commonName.Trim(), organization?.Trim(), organizationalUnit?.Trim(),
            country.Length == 2 ? country.ToUpperInvariant() : string.Empty);

        using var rsa = RSA.Create(keySize);
        var request = new CertificateRequest(new X500DistinguishedName(subject), rsa,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var csrDer = request.CreateSigningRequest();
        var keyDer = string.IsNullOrEmpty(keyPassword)
            ? rsa.ExportPkcs8PrivateKey()
            : rsa.ExportEncryptedPkcs8PrivateKey(keyPassword,
                new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 600_000));

        return new CsrResult(
            PemCodec.Encode("CERTIFICATE REQUEST", csrDer),
            PemCodec.Encode(string.IsNullOrEmpty(keyPassword) ? "PRIVATE KEY" : "ENCRYPTED PRIVATE KEY", keyDer),
            $"RSA {keySize} bits");
    }

    private static string BuildSubject(string commonName, string? organization, string? organizationalUnit, string country)
    {
        var parts = new System.Collections.Generic.List<string> { $"CN={commonName}" };
        if (!string.IsNullOrEmpty(organization)) parts.Add($"O={organization}");
        if (!string.IsNullOrEmpty(organizationalUnit)) parts.Add($"OU={organizationalUnit}");
        if (!string.IsNullOrEmpty(country)) parts.Add($"C={country}");
        return string.Join(", ", parts);
    }
}
