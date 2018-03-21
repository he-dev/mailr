using System;
using System.IO;
using System.Linq;
using System.Linq.Custom;
using JetBrains.Annotations;
using Mailr.Helpers;
using Mailr.Middleware;
using Mailr.Mvc;
using Mailr.Mvc.Razor.ViewLocationExpanders;
using Mailr.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;
using Reusable.AspNetCore.Middleware;
using Reusable.Net.Mail;
using Reusable.OmniLog;
using Reusable.OmniLog.SemanticExtensions;
using Reusable.Utilities.ThirdParty.NLog.LayoutRenderers;

[assembly: AspMvcViewLocationFormat("/src/Views/{1}/{0}.cshtml")]
[assembly: AspMvcViewLocationFormat("/src/Views/Emails/{1}/{0}.cshtml")]
[assembly: AspMvcViewLocationFormat("/src/Views/Shared/{0}.cshtml")]

namespace Mailr
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IHostingEnvironment hostingEnvironment)
        {
            Configuration = configuration;
            HostingEnvironment = hostingEnvironment;
        }

        private IConfiguration Configuration { get; }

        private IHostingEnvironment HostingEnvironment { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            SmartPropertiesLayoutRenderer.Register();

            services.AddSingleton(LoggerFactorySetup.SetupLoggerFactory(HostingEnvironment.EnvironmentName, "MailrAPI", new[] { NLogRx.Create() }));

            services
                .AddMvc()
                .AddPlugins();           

            services.AddScoped<ICssProvider, CssProvider>();
            services.Configure<RazorViewEngineOptions>(options =>
            {
                const string prefix = "src";

                options
                    .ViewLocationExpanders
                    .Add(new RelativeViewLocationExpander(prefix));

                //options
                //    .ViewLocationExpanders
                //    .Add(new ExtensionViewLocationExpander(prefix));
            });

            var emailClient = Configuration["emailClient"];
            switch (emailClient)
            {
                case nameof(SmtpClient):
                    services.AddSingleton<IEmailClient, SmtpClient>();
                    break;

                case nameof(OutlookClient):
                    services.AddSingleton<IEmailClient, OutlookClient>();
                    break;

                default:
                    throw new ArgumentOutOfRangeException($"Invalid EmailClient: {emailClient}. Expected {nameof(SmtpClient)} or {nameof(OutlookClient)}.");
            }

            services.AddSingleton<IHostedService, WorkItemQueueService>();
            services.AddSingleton<IWorkItemQueue, WorkItemQueue>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            const string headerPrefix = "X-Mailr-";

            app.UseSemanticLogger(headerPrefix, product => product is null ? "Unknown" : $"{product}_SemLog3");

            if (env.IsDevelopment())
            {
                app.UseBrowserLink();
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }


            app.UseWhen(httpContext => !httpContext.Request.Method.In(new[] { "GET" }, StringComparer.OrdinalIgnoreCase), UseHeaderValidator());

            app.UseMailer();
            app.UseStaticFiles();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
                //routes.MapRoute(
                //    name: RouteNames.Emails,
                //    template: "wwwroot/css/emails/{controller}/{action}.css");
                routes.MapRoute(
                    name: RouteNames.ExtensionCss,
                    template: "{extension}/wwwroot/css/{controller}/{action}.css");
                routes.MapRoute(
                    name: RouteNames.Themes,
                    template: "wwwroot/css/themes/{name}.css");
            });
        }

        private static Action<IApplicationBuilder> UseHeaderValidator()
        {
            return app =>
            {
                app.UseHeaderValidator("X-Product", "X-Profile");
            };
        }
    }

    internal class RouteNames
    {
        public const string Emails = nameof(Emails);
        public const string ExtensionCss = nameof(ExtensionCss);
        public static string Themes = nameof(Themes);
    }
}
