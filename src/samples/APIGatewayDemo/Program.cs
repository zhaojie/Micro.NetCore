using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ocelot.Middleware;
using Ocelot.Provider.Consul;
using Ocelot.DependencyInjection;
using App.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using App.Metrics.Formatters.InfluxDB;
using App.Metrics.Filtering;

namespace APIGatewayDemo
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
             .UseUrls("http://*:9000")
             .ConfigureAppConfiguration((hostingContext, config) =>
             {
                 config
                     .SetBasePath(hostingContext.HostingEnvironment.ContentRootPath)
                     .AddJsonFile("ocelot.json")
                     .AddEnvironmentVariables();
             })
            .ConfigureServices(services =>
            {
                var filter = new MetricsFilter().WhereType(MetricType.Timer);

                var metrics = AppMetrics.CreateDefaultBuilder().Configuration.Configure(options =>
                {
                    options.AddAppTag("APIGateway");
                    options.AddEnvTag("Development");

                }).Report.ToInfluxDb(options =>
                {
                    options.InfluxDb.BaseUri = new Uri("http://127.0.0.1:8086");
                    options.InfluxDb.Database = "APIGatewayDemo";
                    options.InfluxDb.UserName = "admin";
                    options.InfluxDb.Password = "admin";
                    options.InfluxDb.CreateDataBaseIfNotExists = true;
                    options.HttpPolicy.BackoffPeriod = TimeSpan.FromSeconds(30);
                    options.HttpPolicy.FailuresBeforeBackoff = 5;
                    options.HttpPolicy.Timeout = TimeSpan.FromSeconds(10);
                    options.MetricsOutputFormatter = new MetricsInfluxDbLineProtocolOutputFormatter();
                    options.Filter = filter;
                    options.FlushInterval = TimeSpan.FromSeconds(20);

                }).Build();

                services.AddMetrics(metrics);
                services.AddMetricsReportingHostedService();
                services.AddMetricsTrackingMiddleware();
                services.AddMetricsEndpoints();

                services.AddMetrics().AddOcelot().AddConsul();
            })
            .Configure(app =>
            {
                app.UseMetricsErrorTrackingMiddleware();
                app.UseMetricsAllEndpoints();
                app.UseMetricsAllMiddleware();
                app.UseOcelot().Wait();
            });
    }
}
