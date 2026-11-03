using System;
using System.IO;
using System.Reflection;

namespace EadaptorNextPing.Services;

public static class AppStore
{
    public static string CertificatesFolder { get; private set; } =
        Path.Combine(AppContext.BaseDirectory, "App_Data", "certs");

    public static void Initialize(string contentRootPath)
    {
        CertificatesFolder = Path.Combine(contentRootPath, "App_Data", "certs");
        Directory.CreateDirectory(Path.Combine(contentRootPath, "App_Data"));
    }

    public static string LoadSampleXml()
    {
        var assembly = typeof(AppStore).Assembly;
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.StartsWith("EadaptorNextPing.Templates.", StringComparison.Ordinal));
        if (name is null)
            return string.Empty;
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
