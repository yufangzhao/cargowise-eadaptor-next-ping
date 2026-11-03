using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using EadaptorNextPing.Models;

namespace EadaptorNextPing.Services;

public static class CertLoader
{
    public static bool IsPemText(string value)
        => !string.IsNullOrWhiteSpace(value) && value.Contains("-----BEGIN", StringComparison.Ordinal);

    public static X509Certificate2 LoadForAuth(AppSettings settings)
    {
        if (settings.CertSource == CertSource.Pfx)
        {
            if (string.IsNullOrWhiteSpace(settings.PfxPath))
                throw new ArgumentException("Select a PFX certificate file first.");
            if (!File.Exists(settings.PfxPath))
                throw new ArgumentException($"Certificate file not found: {settings.PfxPath}");

            var pfx = new X509Certificate2(settings.PfxPath, settings.PfxPassword ?? string.Empty,
                X509KeyStorageFlags.EphemeralKeySet);
            EnsureRsaKey(pfx);
            return pfx;
        }

        var certValue = settings.CertificatePath?.Trim() ?? string.Empty;
        var keyValue = settings.PrivateKeyPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(certValue))
            throw new ArgumentException("Provide the certificate: a server file path or pasted PEM text.");
        if (string.IsNullOrWhiteSpace(keyValue))
            throw new ArgumentException("Provide the private key: a server file path or pasted PEM text.");

        var cert = LoadCertificate(certValue);
        using var key = LoadRsaKey(keyValue, settings.KeyPassword);
        return cert.CopyWithPrivateKey(key);
    }

    public static X509Certificate2 LoadCertificate(string pathOrPem)
    {
        if (IsPemText(pathOrPem))
        {
            var der = PemCodec.Decode(pathOrPem, "CERTIFICATE");
            if (der is null)
                throw new ArgumentException("The pasted certificate text does not contain a CERTIFICATE PEM block.");
            return new X509Certificate2(der);
        }

        if (!File.Exists(pathOrPem))
            throw new ArgumentException($"Certificate file not found: {pathOrPem}");

        var ext = Path.GetExtension(pathOrPem).ToLowerInvariant();
        if (ext is ".pfx" or ".p12")
            return new X509Certificate2(pathOrPem, string.Empty, X509KeyStorageFlags.EphemeralKeySet);

        var derFromFile = PemCodec.Decode(File.ReadAllText(pathOrPem), "CERTIFICATE");
        if (derFromFile is not null)
            return new X509Certificate2(derFromFile);
        return new X509Certificate2(File.ReadAllBytes(pathOrPem));
    }

    public static RSA LoadRsaKey(string pathOrPem, string? password)
    {
        var text = IsPemText(pathOrPem)
            ? pathOrPem
            : ReadKeyFile(pathOrPem);

        if (string.IsNullOrEmpty(password))
        {
            var rsa = RSA.Create();
            try
            {
                rsa.ImportFromPem(text);
                return rsa;
            }
            catch (Exception)
            {
                rsa.Dispose();
            }
        }
        else
        {
            var der = PemCodec.Decode(text, "ENCRYPTED PRIVATE KEY");
            if (der is not null)
            {
                var rsa = RSA.Create();
                try
                {
                    rsa.ImportEncryptedPkcs8PrivateKey(password.AsSpan(), der.AsSpan(), out _);
                    return rsa;
                }
                catch (Exception)
                {
                    rsa.Dispose();
                }
            }
        }

        throw new ArgumentException(
            "The private key could not be parsed. Supported: PKCS#1 (RSA PRIVATE KEY), PKCS#8 (PRIVATE KEY) and encrypted PKCS#8 PEM.");
    }

    private static string ReadKeyFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Provide the private key: a server file path or pasted PEM text.");
        if (!File.Exists(path))
            throw new ArgumentException($"Private key file not found: {path}");
        return File.ReadAllText(path);
    }

    private static void EnsureRsaKey(X509Certificate2 certificate)
    {
        if (certificate.GetRSAPrivateKey() is null)
            throw new ArgumentException(
                "The certificate must contain an RSA private key - the JWT client assertion is signed with RS256.");
    }
}

public static class PemCodec
{
    public static string Encode(string label, byte[] data)
    {
        var base64 = Convert.ToBase64String(data);
        var sb = new StringBuilder();
        sb.Append("-----BEGIN ").Append(label).AppendLine("-----");
        for (var i = 0; i < base64.Length; i += 64)
        {
            var len = Math.Min(64, base64.Length - i);
            sb.Append(base64, i, len).Append('\n');
        }
        sb.Append("-----END ").Append(label).AppendLine("-----");
        return sb.ToString();
    }

    public static byte[]? Decode(string text, params string[] labels)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var label = string.Empty;
        var base64 = new StringBuilder();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim().TrimEnd('\r');
            if (line.StartsWith("-----BEGIN ", StringComparison.Ordinal))
            {
                label = line.Substring(11).TrimEnd('-');
                base64.Clear();
            }
            else if (line.StartsWith("-----END ", StringComparison.Ordinal) && label.Length > 0)
            {
                if (labels.Length == 0 || labels.Contains(label, StringComparer.Ordinal))
                {
                    try
                    {
                        return Convert.FromBase64String(base64.ToString());
                    }
                    catch (FormatException)
                    {
                        // keep scanning; a later block may be valid
                    }
                }
                label = string.Empty;
            }
            else if (label.Length > 0 && line.Length > 0 && !line.StartsWith("-----", StringComparison.Ordinal))
            {
                base64.Append(line.Trim());
            }
        }
        return null;
    }
}
