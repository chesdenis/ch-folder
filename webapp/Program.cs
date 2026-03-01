using shared_csharp.Abstractions;
using shared_csharp.Infrastructure;
using webapp.Hubs;
using webapp.Models;
using webapp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddHttpClient();

// app services
builder.Services.Configure<WebAppOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.Configure<ConnectionStringOptions>(builder.Configuration.GetSection("ConnectionStrings"));
builder.Services.AddSingleton<IJobRunner, JobRunner>();
builder.Services.AddSingleton<IDockerPartitionRunner, DockerPartitionRunner>();
builder.Services.AddSingleton<IDockerSearchRunner, DockerSearchRunner>();
builder.Services.AddSingleton<ISearchResultsRepository, SearchResultsRepository>();
builder.Services.AddSingleton<ISearchSessionSelectionRepository, SearchSessionSelectionRepository>();
builder.Services.AddSingleton<ISearchSessionRepository, SearchSessionRepository>();
builder.Services.AddSingleton<IImageLocator, ImageLocator>();
builder.Services.AddSingleton<IContentProvider, RemoteContentProvider>(x=>
    new RemoteContentProvider(x.GetService<IHttpClientFactory>(), 
        builder.Configuration.GetSection("Storage:RemoteStorageBaseUrl").Get<string>()));
builder.Services.AddSingleton<IContentValidationRepository, ContentValidationRepository>();
builder.Services.AddSingleton<IPublishTrackerRepository, PublishTrackerRepository>();

var app = builder.Build();

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