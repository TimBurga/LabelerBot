using LabelerBot.Api;
using LabelerBot.Data;
using LabelerBot.Data.Entities;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContextFactory<DataContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("Database"));
});

builder.Services.AddTransient<IBotService, BotService>();
builder.Services.AddTransient<IDataRepository, DataRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.AddLabelerBotEndpoints();

app.UseHttpsRedirection();

app.UseAuthorization();

//app.MapControllers();

app.Run();