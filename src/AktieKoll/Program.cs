using AktieKoll.Data;
using AktieKoll.Interfaces;
using AktieKoll.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNextApp", policy =>
    {
        policy
          .WithOrigins("http://localhost:3000")    // your Next.js dev URL
          .AllowAnyMethod()
          .AllowAnyHeader();
    });
});


// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgresConnection")));

// Register the service for dependency injection.
builder.Services.AddScoped<IInsiderTradeService, InsiderTradeService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
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
 