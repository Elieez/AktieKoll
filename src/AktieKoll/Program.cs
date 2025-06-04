using AktieKoll.Data;
using AktieKoll.Interfaces;
using AktieKoll.Services;
using CsvHelper;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using AktieKoll.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgresConnection")));

builder.Services.AddSingleton<Func<TextReader, CsvReader>>(provider => reader =>
{
    var config = new CsvHelper.Configuration.CsvConfiguration(new CultureInfo("sv-SE"))
    {
        Delimiter = ";"
    };
    return new CsvReader(reader, config);
});

builder.Services.AddHttpClient<CsvFetchService>();

// Register the service for dependency injection.
builder.Services.AddScoped<IInsiderTradeService, InsiderTradeService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();

app.Run();
