using System;
using System.Globalization;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NLog.Extensions.Logging;

using Sanctuary.Core.Extensions;
using Sanctuary.Database;
using Sanctuary.WebAPI.Endpoints;
using Sanctuary.WebAPI.Options;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls();

// Proxy Server / Load Balancer
var forwardedHeaderSection = builder.Configuration.GetSection("ForwardedHeadersOptions");

if (forwardedHeaderSection is not null)
    builder.Services.Configure<ForwardedHeadersOptions>(forwardedHeaderSection);

// Options
builder.Services.AddOptionsWithValidateOnStart<DatabaseOptions>()
    .BindConfiguration(DatabaseOptions.Section)
    .ValidateOnStart();

builder.Services.AddOptionsWithValidateOnStart<WebAPIOptions>()
    .BindConfiguration(WebAPIOptions.Section)
    .ValidateOnStart();

// Database
builder.Services.AddDatabase(builder.Configuration);

// Logging
builder.Logging.ClearProviders();

#if DEBUG

builder.Logging.SetMinimumLevel(LogLevel.Debug);

builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = HttpLoggingFields.All;
});

#endif

var nlogConfigFile = builder.Environment.IsDevelopment()
    ? "NLog.Development.config"
    : "NLog.config";

builder.Logging.AddNLog(nlogConfigFile);

var app = builder.Build();

#if DEBUG

app.UseHttpLogging();

#endif

// Configure the HTTP request pipeline.

// Local server discovery for OSFR Launcher 1.1.5 (manifest version 2).
app.MapGet("/servermanifest.xml", () => Results.Text("""
    <?xml version="1.0" encoding="utf-8"?>
    <ServerManifest version="2">
      <Name>Sanctuary Local</Name>
      <Description>Local Sanctuary server</Description>
      <WebApiUrl>http://127.0.0.1:20040</WebApiUrl>
      <LoginServer>127.0.0.1:20042</LoginServer>
    </ServerManifest>
    """, "application/xml"));

app.MapGet("/clientmanifest.xml", () => Results.Redirect(
    "https://opensourcefreerealms.com/clientmanifest.xml", permanent: false));

app.MapGet("/client/{**path}", (string? path) =>
{
    if (string.IsNullOrEmpty(path) || path.Contains('\\'))
        return Results.BadRequest();

    var segments = path.Split('/');

    if (Array.Exists(segments, segment => segment is "" or "." or ".."))
        return Results.BadRequest();

    var encodedPath = string.Join("/", Array.ConvertAll(segments, Uri.EscapeDataString));

    return Results.Redirect($"https://opensourcefreerealms.com/client/{encodedPath}", permanent: false);
});

app.MapAuthEndpoints();
app.MapPortraitEndpoints();

app.Run();
