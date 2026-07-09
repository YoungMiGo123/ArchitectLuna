var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // Without this, launching via "dotnet ArchitectLuna.Ui.dll" from a different working
    // directory (any CI/production launch pattern other than "dotnet run") resolves
    // ContentRootPath from the process's cwd instead of the app's own location, silently
    // failing to find wwwroot/.
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();
