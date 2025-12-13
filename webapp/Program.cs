using webapp.Hubs;
using webapp.Models;
using webapp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// app services
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.Configure<ConnectionStringOptions>(builder.Configuration.GetSection("ConnectionStrings"));
builder.Services.AddSingleton<IJobRunner, JobRunner>();
builder.Services.AddSingleton<IDockerFolderRunner, DockerFolderRunner>();
builder.Services.AddSingleton<IDockerSearchRunner, DockerSearchRunner>();
builder.Services.AddSingleton<ISearchResultsRepository, SearchResultsRepository>();
builder.Services.AddSingleton<IImageLocator, ImageLocator>();

var app = builder.Build();

Console.WriteLine("Building index...");
app.Services.GetRequiredService<IImageLocator>().IdentifyImageLocations().Wait();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// SignalR hubs
app.MapHub<JobStatusHub>("/hubs/jobstatus");

app.Run();