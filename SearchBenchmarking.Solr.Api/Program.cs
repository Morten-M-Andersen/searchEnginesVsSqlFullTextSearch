using Microsoft.OpenApi.Models;
//using Microsoft.Extensions.DependencyInjection;
using SolrNet.Microsoft.DependencyInjection; // Til SolrNet integration, specifikt "builder.Services.AddSolrNet<T>()"
using SearchBenchmarking.Library.Interfaces; // Til ISearchService
using SearchBenchmarking.Solr.Api.Documents; // Til SparePartSolrDocument
using SearchBenchmarking.Solr.Api.Services;  // Til SolrSearchService
using SolrNet; // Hoved SolrNet namespace
//using SolrNet.Impl; // Hvis du bruger specifikke Core dele, ofte ikke nødvendigt direkte her

var builder = WebApplication.CreateBuilder(args);

// --- Hent konfiguration ---
var solrConfiguration = builder.Configuration.GetSection("Solr");
string solrUrl = solrConfiguration.GetValue<string>("Url") ?? "http://localhost:8983/solr";
string coreName = solrConfiguration.GetValue<string>("CoreName") ?? "spareparts"; // Eller dit specifikke core navn
string solrFullUrl = $"{solrUrl}/{coreName}";

// --- Konfigurer og tilføj tjenester til containeren ---

// 1. Tilføj SolrNet
// Dette registrerer ISolrOperations<SparePartSolrDocument> og andre nødvendige SolrNet services.
// Det antager, at der er defineret en SparePartSolrDocument klasse, der mapper til felterne i Solr.
builder.Services.AddSolrNet<SparePartSolrDocument>(solrFullUrl);

// Hvis du vil have mere kontrol over SolrNet-initialiseringen, kan du gøre det manuelt:
//builder.Services.AddSingleton<ISolrConnection>(new SolrConnection(solrFullUrl));
//builder.Services.AddScoped<ISolrBasicOperations<SparePartSolrDocument>, SolrBasicServer<SparePartSolrDocument>>();
//builder.Services.AddScoped<ISolrOperations<SparePartSolrDocument>, SolrServer<SparePartSolrDocument>>();
// Men AddSolrNet<T>() er den nemmeste og mest almindelige måde.

// 2. Tilføj din custom Search Service implementering
// Registrerer SolrSearchService som den konkrete implementering af ISearchService.
// Scoped er et godt valg for services, der kan have afhængigheder som DbContexts eller SolrOperations.
builder.Services.AddScoped<ISearchService, SolrSearchService>();

// 3. Tilføj Controller services
builder.Services.AddControllers();

// 4. Tilføj Swagger/OpenAPI for API dokumentation
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

// 5. Konfigurer CORS (Cross-Origin Resource Sharing)
// Dette er et simpelt "AllowAll" eksempel. I produktion bør du være mere specifik.
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

// 6. Tilføj Application Insights Telemetry (Valgfrit, men godt for overvågning)
// builder.Services.AddApplicationInsightsTelemetry(); // Husk at tilføje NuGet pakke og konfiguration


// --- Byg applikationen ---
var app = builder.Build();

// --- Konfigurer HTTP request pipeline ---

// 1. Brug Swagger i Development miljøet
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Search Benchmarking - Solr API V1");
        // c.RoutePrefix = string.Empty; // Sæt Swagger UI til app root, hvis ønsket
    });
}

// 2. Brug HTTPS Redirection (Anbefalet for produktion)
// Sørg for at din Docker container/reverse proxy håndterer SSL terminering,
// eller at Kestrel er konfigureret til HTTPS.
// app.UseHttpsRedirection();

// 3. Brug CORS
app.UseCors(corsPolicyName);

// 4. Brug Routing (Nødvendig for at controllers virker)
app.UseRouting(); // Skal komme før UseAuthorization og UseEndpoints/MapControllers

// 5. Brug Authorization (Hvis du implementerer det)
// app.UseAuthentication(); // Hvis du har authentication middleware
// app.UseAuthorization();

// 6. Map Controllers til endpoints
app.MapControllers();

// --- Log startup information ---
app.Logger.LogInformation("Solr API ({ApplicationName}) is starting up.", builder.Environment.ApplicationName);
app.Logger.LogInformation("Solr Instance URL: {SolrUrl}", solrFullUrl);
app.Logger.LogInformation("Environment: {EnvironmentName}", builder.Environment.EnvironmentName);

// --- Kør applikationen ---
app.Run();