using Microsoft.Extensions.Diagnostics.HealthChecks;
using XamlBrewer.WinUI3.Grpc.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddGrpcHealthChecks()
                .AddCheck("Proforma", () => HealthCheckResult.Healthy());

// Replace the previous call with this, to see 'unknown' in the heartbeat result.
// builder.Services.AddGrpcHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<TransporterService>();
// Comment the next call to see the 'not implemented' in the heartbeat result:
app.MapGrpcHealthChecksService();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
