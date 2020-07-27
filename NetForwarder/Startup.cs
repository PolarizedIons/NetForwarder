using System;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetForwarder.Services;
using Quartz;
using Serilog;

namespace NetForwarder
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<HttpClient>();
            services.AddSingleton<ForwarderManager>();

            services.AddQuartz(q =>
            {
                q.SchedulerId = "NetForwarder";
                q.SchedulerName = "NetForwarder - Quartz Scheduler";
                
                q.UseMicrosoftDependencyInjectionScopedJobFactory(options =>
                {
                    options.AllowDefaultConstructor = true;
                });

                q.UseSimpleTypeLoader();
                q.UseInMemoryStore();
                q.UseDefaultThreadPool(tp =>
                {
                    tp.MaxConcurrency = 10;
                });
                
                q.AddJob<IPWhitelistUpdater>(j => j
                    .WithIdentity("UpdateIPAddressWhitelists")
                );

                q.AddTrigger(t => t
                    .WithIdentity("UpdateIPAddressWhitelistsTrigger")    
                    .ForJob("UpdateIPAddressWhitelists")
                    .StartNow()
                    .WithSimpleSchedule(x => x
                        .WithInterval(TimeSpan.FromMinutes(int.Parse(Configuration["Whitelist:UpdateInterval"])))
                        .RepeatForever()
                    )
                );
            });
            
            services.AddQuartzServer(options =>
            {
                options.WaitForJobsToComplete = true;
            });
            
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseSerilogRequestLogging();
            
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}
