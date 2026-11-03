using System;
using System.Text.Json.Serialization;
using EadaptorNextPing.Models;
using EadaptorNextPing.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

AppStore.Initialize(builder.Environment.ContentRootPath);

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/settings", () => Results.Json(new AppSettings()));

app.MapPost("/api/test", async (AppSettings settings) =>
{
    try
    {
        var token = await CargoClient.TestAuthAsync(settings);
        AppServicesLog.Info("Authentication succeeded - token expires in " + token.ExpiresInText);
        return Results.Ok(new { ok = true, expiresIn = token.ExpiresInText });
    }
    catch (Exception ex)
    {
        AppServicesLog.Error("Authentication failed: " + ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/send", async (SendEnvelope envelope) =>
{
    var result = await CargoClient.SendAsync(envelope.Settings, envelope.Body ?? string.Empty);
    if (result.HasError)
        AppServicesLog.Error("Send failed: " + result.Error);
    else
        AppServicesLog.Info($"POST -> HTTP {result.StatusCode} in {result.DurationText}" +
                            (string.IsNullOrEmpty(result.UniversalStatus) ? "" : $", UniversalResponse Status={result.UniversalStatus}"));
    return Results.Ok(result);
});

app.MapPost("/api/format", (FormatRequest request) =>
{
    var formatted = XmlHelper.TryFormat(request.Xml ?? string.Empty);
    return formatted is null
        ? Results.BadRequest(new { error = "The text is not well-formed XML." })
        : Results.Ok(new { formatted });
});

app.MapGet("/api/sample", () => Results.Ok(new { body = AppStore.LoadSampleXml() }));

app.MapPost("/api/csr", (CsrRequestModel request) =>
{
    try
    {
        var result = CsrGenerator.Generate(request.CommonName, request.Organization, request.OrganizationalUnit,
            request.Country, request.KeySize, string.IsNullOrEmpty(request.KeyPassword) ? null : request.KeyPassword);
        return Results.Ok(new { result.CsrPem, result.KeyPem, result.KeyAlgorithm });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();

public sealed record SendEnvelope(AppSettings Settings, string? Body);
public sealed record FormatRequest(string? Xml);
public sealed record CsrRequestModel(string CommonName, string Organization, string OrganizationalUnit,
    string Country, int KeySize, string? KeyPassword);

public static class AppServicesLog
{
    public static void Info(string message) => Console.WriteLine("[info ] " + DateTime.Now.ToString("HH:mm:ss") + " " + message);
    public static void Error(string message) => Console.Error.WriteLine("[error] " + DateTime.Now.ToString("HH:mm:ss") + " " + message);
}
