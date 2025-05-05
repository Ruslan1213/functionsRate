using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

var host = new HostBuilder()
    .ConfigureWebJobs(builder =>
    {
        builder.AddDurableTask();
        builder.AddHttp();
    })
    .ConfigureServices(services =>
    {
        services.AddHttpClient();
    })
    .ConfigureLogging(logging => logging.AddConsole())
    .Build();

host.Run();
