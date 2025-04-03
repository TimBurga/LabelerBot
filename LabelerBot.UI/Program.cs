using LabelerBot.Data.Entities;
using LabelerBot.UI.Components;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();


//using LabelerBot.Data.Entities;
//using LabelerBot.UI.Components;
//using Microsoft.AspNetCore.Authentication;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc.Authorization;
//using Microsoft.AspNetCore.Server.HttpSys;
//using Microsoft.EntityFrameworkCore;

//var builder = WebApplication.CreateBuilder(args);

////builder.AddServiceDefaults();
////builder.Services.AddAuthentication().AddBearerToken();

////builder.Services.AddControllersWithViews(options =>
////{
////    var policy = new AuthorizationPolicyBuilder()
////        .RequireAuthenticatedUser()
////        .Build();
////    options.Filters.Add(new AuthorizeFilter(policy));
////});

//// Add services to the container.
//builder.Services.AddRazorComponents()
//    .AddInteractiveServerComponents();





//var app = builder.Build();

//// Configure the HTTP request pipeline.
//if (!app.Environment.IsDevelopment())
//{
//    app.UseExceptionHandler("/Error", createScopeForErrors: true);
//    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
//    //app.UseHsts();
//}

////app.UseHttpsRedirection();

////app.MapDefaultEndpoints();

//app.UseAntiforgery();
////app.MapControllers();

//app.MapStaticAssets();
//app.MapRazorComponents<App>()
//    .AddInteractiveServerRenderMode();

//app.Run();
