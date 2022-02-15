using Microsoft.AspNetCore.ResponseCompression;
using System.Net;
using WordleOff.Server.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
#region snippet_ConfigureServices
builder.Services.AddSignalR();
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddResponseCompression(opts =>
{
  opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/octet-stream" });
});
#endregion

builder.Services.AddHttpsRedirection(options =>
{
  options.RedirectStatusCode = (int)HttpStatusCode.TemporaryRedirect;
  options.HttpsPort = 5001;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
#region snippet_Configure
app.UseResponseCompression();

if (app.Environment.IsDevelopment())
{
  app.UseWebAssemblyDebugging();
}
else
{
  app.UseExceptionHandler("/Error");
  app.UseHsts();
}

app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.MapRazorPages();
app.MapControllers();
app.MapHub<WordleOffHub>("/WordleOffHub");
app.MapFallbackToFile("index.html");

app.Run();
#endregion
