using InternetTracer.Data;
using InternetTracer.Monitor;
using InternetTracer.Service;
using InternetTracer.Ipc;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<DatabaseFactory>();
builder.Services.AddSingleton<SchemaMigrationEngine>();
builder.Services.AddSingleton<MinuteAggregator>();
builder.Services.AddSingleton<WindowsNetworkInterfaceMonitor>();
builder.Services.AddSingleton<TrafficDeltaCalculator>();

// Provide live snapshot from the DeltaCalculator (or keep it simple for now)
builder.Services.AddSingleton<InternetTracer.Core.Contracts.ITelemetryServiceApi>(sp => 
{
    var dbFactory = sp.GetRequiredService<DatabaseFactory>();
    return new SqliteTelemetryQueryService(dbFactory, () => new InternetTracer.Core.Contracts.CurrentSnapshot());
});

builder.Services.AddSingleton<IpcServer>(sp => 
{
    var api = sp.GetRequiredService<InternetTracer.Core.Contracts.ITelemetryServiceApi>();
    // Fast-path: authorize current user (which is the one running the service, but wait, Service runs as LocalSystem. We need to pass the Interactive User SID or rely on BuiltinAdministratorsSid logic).
    // For now, we'll allow Administrators in IpcServer, so just passing BuiltinAdministratorsSid works.
    var adminSid = new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.BuiltinAdministratorsSid, null);
    return new IpcServer(api, adminSid);
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// Run migrations on startup
var migrator = host.Services.GetRequiredService<SchemaMigrationEngine>();
migrator.Migrate();

host.Run();
