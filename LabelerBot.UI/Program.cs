using LabelerBot.Data.Entities;
using LabelerBot.UI.Components;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();


if (builder.Environment.IsProduction())
{
    IConfigurationSection azureAdSection = builder.Configuration.GetSection("AzureAd");

    azureAdSection.GetSection("Instance").Value = "https://login.microsoftonline.com/";
    azureAdSection.GetSection("Domain").Value = Environment.GetEnvironmentVariable("AzureAd__Domain");
    azureAdSection.GetSection("TenantId").Value = Environment.GetEnvironmentVariable("AzureAd__TenantId");
    azureAdSection.GetSection("ClientId").Value = Environment.GetEnvironmentVariable("AzureAd__ClientId");
    azureAdSection.GetSection("CallbackPath").Value = Environment.GetEnvironmentVariable("AzureAd__CallbackPath");
    azureAdSection.GetSection("SignedOutCallbackPath").Value = Environment.GetEnvironmentVariable("AzureAd__SignedOutCallbackPath");
}

builder.Services.AddMicrosoftIdentityWebAppAuthentication(builder.Configuration);

builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
}).AddMicrosoftIdentityUI();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContextFactory<DataContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("Database"));
});

builder.Services.AddQuickGridEntityFrameworkAdapter();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.MapDefaultEndpoints();

app.UseAntiforgery();
app.MapControllers();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
