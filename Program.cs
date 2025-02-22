using LabelerBot;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<Worker>();
builder.Services.AddDbContextFactory<DataContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("Database"));
});

builder.Services.AddLogging(x => x.AddConsole());

builder.Services.AddTransient<IDataRepository, DataRepository>();
builder.Services.AddTransient<ILabelService, LabelService>();

builder.Services.AddSingleton<ILabeler, Labeler>();

var host = builder.Build();
host.Run();
