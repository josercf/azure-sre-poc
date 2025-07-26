using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using Microsoft.EntityFrameworkCore;
using AdminPanel.Data;
using AdminPanel.Services;
using AdminPanel.Models;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Entity Framework with In-Memory Database
builder.Services.AddDbContext<AdminPanelDbContext>(options =>
    options.UseInMemoryDatabase("AdminPanelDb"));

// Add custom services - using mock services for in-memory setup
builder.Services.AddScoped<IKeyVaultService, MockKeyVaultService>();
builder.Services.AddScoped<IServiceBusManagementService, MockServiceBusManagementService>();
builder.Services.AddScoped<IClientOnboardingService, ClientOnboardingService>();

// Uncomment below lines when you have real Azure services configured:
// builder.Services.AddScoped<IKeyVaultService, KeyVaultService>();
// builder.Services.AddScoped<IServiceBusManagementService, ServiceBusManagementService>();

// Add Activity Source for custom tracing
builder.Services.AddSingleton(new ActivitySource("AdminPanel.ClientOnboarding"));

// Add OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("admin-panel-service", "1.0.0"))
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .AddSource("AdminPanel.ClientOnboarding")
            .AddAspNetCoreInstrumentation();

        // Add OTLP exporter
        var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        if (!string.IsNullOrEmpty(otlpEndpoint))
        {
            try
            {
                Console.WriteLine($"Configuring OTLP exporter with endpoint: {otlpEndpoint}");
                tracerProviderBuilder.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to configure OTLP exporter: {ex.Message}");
                Console.WriteLine("Continuing without OTLP tracing...");
            }
        }
        else
        {
            Console.WriteLine("OTEL_EXPORTER_OTLP_ENDPOINT not set, using console exporter for tracing");
            tracerProviderBuilder.AddConsoleExporter();
        }
    })
    .WithMetrics(meterProviderBuilder =>
        meterProviderBuilder
            .AddAspNetCoreInstrumentation()
            .AddPrometheusExporter());

var app = builder.Build();

// Ensure database is created and seeded
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AdminPanelDbContext>();
    // In-Memory database doesn't need EnsureCreated, it's created automatically
    await SeedData.SeedAsync(context);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

// Prometheus metrics endpoint
app.UseOpenTelemetryPrometheusScrapingEndpoint();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "admin-panel" }));

// Root endpoint
app.MapGet("/", () => "Admin Panel - Customer Onboarding Service");

// Enable static files
app.UseStaticFiles();

// Onboarding UI endpoint
app.MapGet("/onboarding", () => Results.Redirect("/onboarding.html"));

app.MapControllers();

app.Run();

 