using image_searcher;
using Microsoft.Extensions.DependencyInjection;
using shared_csharp.Abstractions;
using shared_csharp.Infrastructure;

var services = new ServiceCollection();

services.AddSingleton<IContentProvider, RemoteContentProvider>(
    sp => new RemoteContentProvider(sp.GetService<IHttpClientFactory>(), 
        Environment.GetEnvironmentVariable("RCP_ENDPOINT")) );
services.AddSingleton<ImageSearcher>();

var provider = services.BuildServiceProvider();

await provider.GetRequiredService<ImageSearcher>().RunAsync(args);

