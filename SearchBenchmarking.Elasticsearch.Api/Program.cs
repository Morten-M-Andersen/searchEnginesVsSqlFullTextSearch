using Elastic.Clients.Elasticsearch;
using Microsoft.OpenApi.Models;
using SearchBenchmarking.Elasticsearch.Api.Services;
using SearchBenchmarking.Library.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// --- Henter konfiguration ---
var elasticsearchConfiguration = builder.Configuration.GetSection("Elasticsearch");
string elasticsearchUrl = elasticsearchConfiguration.GetValue<string>("Url") ?? "http://localhost:9200";

var clientSettings = new ElasticsearchClientSettings(new Uri(elasticsearchUrl));

var esClient = new ElasticsearchClient(clientSettings);
builder.Services.AddSingleton(esClient);

// Tilføjer custom Search Service implementering
builder.Services.AddScoped<ISearchService, ElasticsearchSearchService>();

builder.Services.AddControllers();

// Tilføjer Swagger/OpenAPI for API dokumentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Search Benchmarking - Elasticsearch API",
        Version = "v1",
        Description = "API for searching an Elasticsearch index."
    });
});

// Konfigurerer CORS (Cross-Origin Resource Sharing)
var corsPolicyName = "AllowAllOrigins"; // Matcher navnet fra appsettings.json, hvis det er der
if (builder.Configuration.GetValue<bool?>("Cors:AllowAll") == true)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(name: corsPolicyName,
                          policyBuilder =>
                          {
                              policyBuilder.AllowAnyOrigin()
                                           .AllowAnyMethod()
                                           .AllowAnyHeader();
                          });
    });
}


var app = builder.Build();

// Brug Swagger i Development miljøet - skal tilføje appsettings.Development.json...
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Search Benchmarking - Elasticsearch API V1");
    });
}


// Brug CORS (hvis konfigureret)
if (builder.Configuration.GetValue<bool?>("Cors:AllowAll") == true)
{
    app.UseCors(corsPolicyName);
}

app.UseRouting();
app.MapControllers();

// --- Log startup information ---
app.Logger.LogInformation("Elasticsearch API ({ApplicationName}) is starting up.", builder.Environment.ApplicationName);
app.Logger.LogInformation("Elasticsearch Instance URL: {ElasticsearchUrl}", elasticsearchUrl);
app.Logger.LogInformation("Environment: {EnvironmentName}", builder.Environment.EnvironmentName);

app.Run();