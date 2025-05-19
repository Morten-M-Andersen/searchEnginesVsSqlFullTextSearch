//var builder = WebApplication.CreateBuilder(args);

//// Add services to the container.

//builder.Services.AddControllers();
//// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

//var app = builder.Build();

//// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}

//app.UseHttpsRedirection();

//app.UseAuthorization();

//app.MapControllers();

//app.Run();


using ElasticSearchAPI;

var builder = WebApplication.CreateBuilder(args);

// Tilføj tjenester til containeren
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Konfigurer ElasticSearch
var elasticUrl = builder.Configuration.GetValue<string>("ElasticSearch:Url") ?? "http://localhost:9200";
var indexName = builder.Configuration.GetValue<string>("ElasticSearch:IndexName") ?? "spareparts";

// Registrer ElasticSearch tjenester
builder.Services.AddSingleton<IElasticSearchService>(sp =>
    new ElasticSearchService(elasticUrl, indexName));

var app = builder.Build();

// Konfigurer HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
