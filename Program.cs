using LabelerBot.Bot;
using LabelerBot.Bot.DataAccess;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<LabelerBot.Bot.LabelerBot>();
builder.Services.AddDbContextFactory<DataContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("Database"));
});

builder.Services.AddLogging(x => x.AddJsonConsole(c => c.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] "));

builder.Services.AddTransient<IDataRepository, DataRepository>();
builder.Services.AddTransient<ILabelService, LabelService>();
builder.Services.AddTransient<ILabeler, OzoneLabeler>();

builder.Services.AddSingleton<IAtProtoSessionManager, AtProtoSessionManager>();

var host = builder.Build();
host.Run();
