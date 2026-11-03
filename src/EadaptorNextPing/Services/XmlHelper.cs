using System;
using System.Linq;
using System.Security;
using System.Xml.Linq;

namespace EadaptorNextPing.Services;

public static class XmlHelper
{
    public static string? TryFormat(string xml)
    {
        try
        {
            using var reader = new StringReader(xml.Trim());
            var doc = XDocument.Load(reader, LoadOptions.None);
            using var writer = new StringWriter();
            doc.Save(writer, SaveOptions.None);
            return writer.ToString();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string? ParseUniversalStatus(string xml)
    {
        try
        {
            using var reader = new StringReader(xml.Trim());
            var doc = XDocument.Load(reader, LoadOptions.None);
            var root = doc.Root;
            if (root is null || !string.Equals(root.Name.LocalName, "UniversalResponse", StringComparison.OrdinalIgnoreCase))
                return null;
            return root.Elements().FirstOrDefault(e => e.Name.LocalName == "Status")?.Value.Trim();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string? ParseProcessingLog(string xml)
    {
        try
        {
            using var reader = new StringReader(xml.Trim());
            var doc = XDocument.Load(reader, LoadOptions.None);
            return doc.Root
                ?.Elements().FirstOrDefault(e => e.Name.LocalName == "ProcessingLog")
                ?.Value.Trim();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string EscapeXml(string value) => SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;
}
