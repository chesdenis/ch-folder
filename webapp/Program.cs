var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// app services
builder.Services.Configure<webapp.Models.StorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.AddSingleton<webapp.Services.IJobRunner, webapp.Services.JobRunner>();
builder.Services.AddSingleton<webapp.Services.IDockerRunner, webapp.Services.DockerRunner>();

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
app.MapHub<webapp.Hubs.JobStatusHub>("/hubs/jobstatus");

app.Run();