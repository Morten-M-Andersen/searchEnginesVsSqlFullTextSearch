using Microsoft.OpenApi.Models;
using SolrNet.Microsoft.DependencyInjection;
using SearchBenchmarking.Library.Interfaces;
using SearchBenchmarking.Solr.Api.Documents;
using SearchBenchmarking.Solr.Api.Services;
using SolrNet;

var builder = WebApplication.CreateBuilder(args);

// --- Hent konfiguration ---
var solrConfiguration = builder.Configuration.GetSection("Solr");
string solrUrl = solrConfiguration.GetValue<string>("Url") ?? "http://localhost:8983/solr";
string coreName = solrConfiguration.GetValue<string>("CoreName") ?? "spareparts"; // Eller dit specifikke core navn
string solrFullUrl = $"{solrUrl}/{coreName}";

// --- Konfigurer og tilføj tjenester til containeren ---

builder.Services.AddSolrNet<SparePartSolrDocument>(solrFullUrl);

// Tilføjer custom Search Service implementering
builder.Services.AddScoped<ISearchService, SolrSearchService>();

builder.Services.AddControllers();

// Tilføjer Swagger/OpenAPI for API dokumentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Search Benchmarking - Solr API",
        Version = "v1",
        Description = "API for searching a Solr index."
    });
});

// Konfigurerer CORS (Cross-Origin Resource Sharing)
var corsPolicyName = "AllowAllOrigins";
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

var app = builder.Build();


// Brug Swagger i Development miljøet - skal tilføje appsettings.Development.json...
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Search Benchmarking - Solr API V1");
        // c.RoutePrefix = string.Empty; // Sæt Swagger UI til app root, hvis ønsket
    });
}

// Brug CORS (hvis konfigureret)
app.UseCors(corsPolicyName);

app.UseRouting();
app.MapControllers();

// --- Log startup information ---
app.Logger.LogInformation("Solr API ({ApplicationName}) is starting up.", builder.Environment.ApplicationName);
app.Logger.LogInformation("Solr Instance URL: {SolrUrl}", solrFullUrl);
app.Logger.LogInformation("Environment: {EnvironmentName}", builder.Environment.EnvironmentName);

app.Run();