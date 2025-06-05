using Microsoft.OpenApi.Models;
using SearchBenchmarking.Library.Interfaces;
using SearchBenchmarking.Sql.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Vores Search Service implementering
builder.Services.AddScoped<ISearchService, SqlSearchService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Search Benchmarking - MSSQL API",
        Version = "v1",
        Description = "API for searching an MSSQL database using Full-Text Search."
    });
});

// (Cross-Origin Resource Sharing)
var corsPolicyName = "AllowAllOrigins";
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

// Kræver at vi tilføjer en Development udgave af appsettings.json...
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Search Benchmarking - MSSQL API V1");
    });
}

if (builder.Configuration.GetValue<bool?>("Cors:AllowAll") == true)
{
    app.UseCors(corsPolicyName);
}

app.UseRouting();
app.MapControllers();

// Log startup information
app.Logger.LogInformation("MSSQL API ({ApplicationName}) is starting up.", builder.Environment.ApplicationName);
app.Logger.LogInformation("Using ConnectionString for 'SparePartsDBConnection'.");
app.Logger.LogInformation("Environment: {EnvironmentName}", builder.Environment.EnvironmentName);

app.Run();