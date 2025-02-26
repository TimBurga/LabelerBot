using LabelerBot;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<Worker>();
builder.Services.AddDbContextFactory<DataContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("Database"));
});

builder.Services.AddLogging(x => x.AddJsonConsole(c => c.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] "));

builder.Services.AddTransient<IDataRepository, DataRepository>();
builder.Services.AddTransient<ILabelService, LabelService>();
builder.Services.AddTransient<ILabeler, Labeler>();

builder.Services.AddSingleton<ILabelerSessionManager, LabelerSessionManager>();

var host = builder.Build();
host.Run();
