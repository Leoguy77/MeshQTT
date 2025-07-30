using System.Buffers;
using MeshQTT.Entities;
using MeshQTT.Managers;
using MeshQTT.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;

namespace MeshQTT
{
    public class Program
    {
        private static Config? config = new(
            Path.Combine(AppContext.BaseDirectory, "config", "config.json")
        );
        private static readonly List<Node> nodes = new();

        static async Task Main(string[] args)
        {
            bool isDebug = false;
#if DEBUG
            isDebug = true;
#endif

            // Start Prometheus metrics server
            MetricsManager.StartPrometheusMetricsServer(isDebug);
            
            // Initialize AlertManager
            AlertManager? alertManager = null;
            if (config != null)
            {
                alertManager = new AlertManager(config);
                
                // Send service start alert
                if (config.Alerting.Enabled)
                {
                    await alertManager.TriggerServiceRestartAlert("Service started");
                }
            }
            
            // Initialize core services
            var messageProcessor = new MessageProcessor(nodes, config, alertManager);
            var mqttServerManager = new MqttServerManager(config, messageProcessor, nodes, alertManager);
            var apiService = new ApiService(nodes, config, alertManager);

            // Build and configure the web host
            var webHost = Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls("http://0.0.0.0:8080");
                    webBuilder.UseStartup<ApiStartup>();
                    webBuilder.ConfigureServices(services =>
                    {
                        services.AddSingleton(apiService);
                    });
                })
                .Build();

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                Logger.Log("Shutdown requested (Ctrl+C pressed)...");
                eventArgs.Cancel = true;
                cts.Cancel();
            };

            // Start both MQTT server and Web API
            var mqttTask = mqttServerManager.StartAsync(cts.Token);
            var webTask = webHost.RunAsync(cts.Token);

            Logger.Log("MeshQTT started with MQTT broker and Management API");
            Logger.Log($"MQTT Port: {config?.Port ?? 1883}");
            if (config?.TlsEnabled == true)
            {
                Logger.Log($"MQTT TLS Port: {config.TlsPort}");
            }
            Logger.Log("Management API: http://localhost:8080");
            Logger.Log("API Documentation: http://localhost:8080/swagger (in debug mode)");

            // Wait for either service to complete
            await Task.WhenAny(mqttTask, webTask);
        }
    }

    public class ApiStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = "MeshQTT Management API",
                    Version = "v1",
                    Description = "REST API for managing MeshQTT broker - list nodes, ban/unban nodes, and manage settings"
                });
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MeshQTT Management API V1");
                    c.RoutePrefix = string.Empty; // Serve Swagger UI at root
                });
            }

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
