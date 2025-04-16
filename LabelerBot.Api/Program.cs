using LabelerBot.Api;
using LabelerBot.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHostedService<LabelerBot.Api.LabelerBot>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContextFactory<DataContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("Database"));
});

builder.Services.AddLogging(x => x.AddJsonConsole(c => c.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] "));

builder.Services.AddTransient<IBotService, BotService>();
builder.Services.AddTransient<IDataRepository, DataRepository>();
builder.Services.AddTransient<ILabelService, LabelService>();
builder.Services.AddTransient<ILabeler, OzoneLabeler>();
builder.Services.AddTransient<IPostService, PostService>();

builder.Services.AddSingleton<IAtProtoSessionManager, AtProtoSessionManager>();
builder.Services.AddSingleton<IJetstreamSessionManager, JetstreamSessionManager>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

//app.AddLabelerBotEndpoints();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();