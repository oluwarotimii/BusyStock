using WatcherService;
using WatcherService.Services;

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "BASM";
    })
    .ConfigureServices((hostContext, services) =>
    {
        // Add configuration
        var config = hostContext.Configuration;

        // Register services
        services.AddScoped<IProductDataService, ProductDataService>();
        services.AddHttpClient<ApiService>();
        services.AddScoped<IApiService, ApiService>();

        // Add the background service
        services.AddHostedService<ProductWatcherWorker>();
    })
    .Build();

await host.RunAsync();