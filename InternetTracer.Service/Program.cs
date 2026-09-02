using InternetTracer.Data;
using InternetTracer.Monitor;
using InternetTracer.Service;
using InternetTracer.Ipc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System;

var builder = Host.CreateApplicationBuilder(args);

var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "InternetTracer");
Directory.CreateDirectory(appData);
var dbPath = Path.Combine(appData, "telemetry.db");

builder.Services.AddSingleton<DatabaseFactory>(sp => new DatabaseFactory(dbPath));
builder.Services.AddSingleton<SchemaMigrationEngine>();
builder.Services.AddSingleton<MinuteAggregator>();
builder.Services.AddSingleton<WindowsNetworkInterfaceMonitor>();
builder.Services.AddSingleton<TrafficDeltaCalculator>();
builder.Services.AddSingleton<LiveTelemetryBuffer>();

// Provide live snapshot from the DeltaCalculator (or keep it simple for now)
builder.Services.AddSingleton<InternetTracer.Core.Contracts.ITelemetryServiceApi>(sp => 
{
    var dbFactory = sp.GetRequiredService<DatabaseFactory>();
    var liveBuffer = sp.GetRequiredService<LiveTelemetryBuffer>();
    return new SqliteTelemetryQueryService(dbFactory, liveBuffer);
});

builder.Services.AddSingleton<IpcServer>(sp => 
{
    var api = sp.GetRequiredService<InternetTracer.Core.Contracts.ITelemetryServiceApi>();
    // Fast-path: authorize current user (which is the one running the service, but wait, Service runs as LocalSystem. We need to pass the Interactive User SID or rely on BuiltinAdministratorsSid logic).
    // Fast-path: authorize current user (which is the one running the service/test).
    var userSid = System.Security.Principal.WindowsIdentity.GetCurrent().User;
    return new IpcServer(api, userSid!);
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// Run migrations on startup
var migrator = host.Services.GetRequiredService<SchemaMigrationEngine>();
migrator.Migrate();

host.Run();
