using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SagaCommerce.Contracts.Services;
using SagaOrchestrator.Data;
using SagaOrchestrator.Models;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddOpenApi();

builder.Services.AddDbContext<SagaDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services
    .AddOptions<ServiceUrlsOptions>()
    .Bind(builder.Configuration.GetSection(ServiceUrlsOptions.SectionName))
    .Validate(
        options =>
            Uri.TryCreate(options.Inventory, UriKind.Absolute, out var inventoryUri)
            && (inventoryUri.Scheme == Uri.UriSchemeHttp || inventoryUri.Scheme == Uri.UriSchemeHttps),
        "Inventory URL must be a valid absolute HTTP/HTTPS URL.")
    .Validate(
        options =>
            Uri.TryCreate(options.Payment, UriKind.Absolute, out var paymentUri)
            && (paymentUri.Scheme == Uri.UriSchemeHttp || paymentUri.Scheme == Uri.UriSchemeHttps),
        "Payment URL must be a valid absolute HTTP/HTTPS URL.")
    .ValidateOnStart();

builder.Services.AddHttpClient("Inventory", (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<ServiceUrlsOptions>>().Value;

    client.BaseAddress = new Uri(options.Inventory);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient("Payment", (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<ServiceUrlsOptions>>().Value;

    client.BaseAddress = new Uri(options.Payment);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddScoped<SagaManager>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.DarkMode = true;
        options.WithTheme(ScalarTheme.DeepSpace);
        options.WithTitle("Saga Orchestrator");
        options.DefaultHttpClient = new();
        options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
        options.WithSearchHotKey("k");
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
