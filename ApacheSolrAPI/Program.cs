using ApacheSolrAPI;
using SolrNet;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Tilføj tjenester til containeren
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Apache Solr Search API",
        Version = "v1",
        Description = "API for searching spare parts using Apache Solr"
    });
});

// Konfigurer Solr
var solrUrl = builder.Configuration.GetValue<string>("Solr:Url") ?? "http://localhost:8983/solr";
var coreName = builder.Configuration.GetValue<string>("Solr:CoreName") ?? "spareparts";

// Registrer SolrNet
var solrConnection = $"{solrUrl}/{coreName}";
builder.Services.AddSolrNet<SparePartDocument>(solrConnection);

// Registrer Solr service
builder.Services.AddScoped<SolrSearchService>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

var app = builder.Build();

// Konfigurer HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Apache Solr Search API V1");
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Log startup info
app.Logger.LogInformation($"Solr API started. Connected to: {solrConnection}");

app.Run();