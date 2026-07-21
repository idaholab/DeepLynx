using deeplynx.blobhash.functions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddSingleton(BlobHashSettings.FromEnvironment());
        services.AddSingleton<BlobNameParser>();
        services.AddSingleton<BlobHashComputer>();
        services.AddHttpClient<NexusTokenClient>();
        services.AddHttpClient<NexusBlobHashClient>();
    })
    .Build();

host.Run();
