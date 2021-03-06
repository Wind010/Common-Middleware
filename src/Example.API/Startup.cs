using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


namespace Example.API
{
    using Common.Middleware.Extensions;

    using Serilog;

    public class Startup
    {
        private IConfiguration _configuration;
        private ILogger _logger;


        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }


        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            _logger = services.AddSerilog(_configuration);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Add any correlationId enforcement.
            app.ConfigureRequestResponseLoggingMiddleware(_logger);
            app.ConfigureExceptionHandlingMiddleware(_logger);

            app.UseHttpsRedirection();

            // If needed:
            // app.AddAuthentication();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
