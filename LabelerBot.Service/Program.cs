using LabelerBot;
using LabelerBot.Data;
using LabelerBot.Data.Entities;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<LabelerBot.Service.LabelerBot>();
builder.Services.AddDbContextFactory<DataContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("Database"));
});

builder.AddServiceDefaults();

builder.Services.AddLogging(x => x.AddJsonConsole(c => c.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] "));

builder.Services.AddTransient<IDataRepository, DataRepository>();
builder.Services.AddTransient<ILabelService, LabelService>();
builder.Services.AddTransient<ILabeler, OzoneLabeler>();
builder.Services.AddTransient<IPostService, PostService>();

builder.Services.AddSingleton<IAtProtoSessionManager, AtProtoSessionManager>();
builder.Services.AddSingleton<IJetstreamSessionManager, JetstreamSessionManager>();

var host = builder.Build();
host.Run();
