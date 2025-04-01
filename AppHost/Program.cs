var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.LabelerBot_Service>("bot");
builder.AddProject<Projects.LabelerBot_UI>("ui");

builder.Build().Run();
