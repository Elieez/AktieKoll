using AktieKoll.Data;
using AktieKoll.Interfaces;
using AktieKoll.Services;
using CsvHelper;
using System.Globalization;
using System.Linq;
using AktieKoll.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNextApp", policy =>
    {
        policy
          .WithOrigins("http://localhost:3000")
          .AllowAnyMethod()
          .AllowAnyHeader();
    });
});

builder.Services.AddControllers();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgresConnection")));

//builder.Services.AddSingleton<Func<TextReader, CsvReader>>(provider => reader =>
//{
//    var config = new CsvHelper.Configuration.CsvConfiguration(new CultureInfo("sv-SE"))
//    {
//        Delimiter = ";"
//    };
//    return new CsvReader(reader, config);
//});

builder.Services.AddHttpClient<CsvFetchService>();

builder.Services.AddScoped<IInsiderTradeService, InsiderTradeService>();

var app = builder.Build();

var httpClient = new HttpClient();

//CsvReader csvReaderFactory(TextReader reader)
//{
//    var config = new CsvHelper.Configuration.CsvConfiguration(new CultureInfo("sv-SE"))
//    {
//        Delimiter = ";"
//    };
//    return new CsvReader(reader, config);
//}

//var csvService = new CsvFetchService(httpClient, csvReaderFactory);

//// Fetch new insider trades at startup and persist them
//app.Lifetime.ApplicationStarted.Register(async () =>
//{
//    var csvResults = await csvService.FetchInsiderTradesAsync();
//    var trades = csvResults.Select(dto => dto.ToInsiderTrade()).ToList();

//    var tradeService = app.Services.GetRequiredService<IInsiderTradeService>();
//    var message = await tradeService.AddInsiderTrades(trades);
//    Console.WriteLine(message);
//});

if (!app.Environment.IsDevelopment())
{ 
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseCors("AllowNextApp");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();

app.Run();
 