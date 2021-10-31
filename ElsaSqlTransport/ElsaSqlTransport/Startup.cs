using Elsa;
using Elsa.Caching.Rebus.Extensions;
using Elsa.Persistence.EntityFramework.Core.Extensions;
using Elsa.Persistence.EntityFramework.SqlServer;
using ElsaSqlTransport.Workflow;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Rebus.Config;
using System;

namespace ElsaSqlTransport
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            var dbConnectionString = Configuration.GetConnectionString("Sql") ?? throw new InvalidOperationException();

            services.AddElsa(elsa =>
            {
                elsa.AddWorkflowsFrom<FirstWorkflow>();
                elsa.AddActivitiesFrom<Step1>();

                // Elsa Distributed Hosting
                // https://elsa-workflows.github.io/elsa-core/docs/hosting/hosting-distributed-hosting

                // Elsa persistence
                elsa.UseEntityFrameworkPersistence(ef => DbContextOptionsBuilderExtensions.UseSqlServer(ef, dbConnectionString));

                // Distributed lock
                elsa.ConfigureDistributedLockProvider(options => options.UseSqlServerLockProvider(dbConnectionString));

                // Rebus
                elsa.UseServiceBus(bus =>
                {
                    bus.Configurer
                        .Transport(t => t.UseSqlServer(new SqlServerTransportOptions(dbConnectionString), bus.QueueName))
                        .Subscriptions(s => s.StoreInSqlServer(dbConnectionString, "ElsaQueueSubscriptions"));
                });

                // Distributed Cache Signal Provider
                elsa.UseRebusCacheSignal();

                // Hangfire
                elsa.AddHangfireTemporalActivities(hangfire => hangfire.UseSqlServerStorage(dbConnectionString));
            });

            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "ElsaSqlTransport", Version = "v1" });
            });
        }
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ElsaSqlTransport v1"));
            }

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
