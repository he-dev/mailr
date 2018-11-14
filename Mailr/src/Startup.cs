using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Custom;
using JetBrains.Annotations;
using Mailr.Extensions;
using Mailr.Helpers;
using Mailr.Middleware;
using Mailr.Mvc;
using Mailr.Mvc.Razor.ViewLocationExpanders;
using Mailr.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Reusable;
using Reusable.AspNetCore.Http;
using Reusable.Net.Mail;
using Reusable.OmniLog;
using Reusable.OmniLog.Attachements;
using Reusable.OmniLog.SemanticExtensions;
using Reusable.Utilities.AspNetCore.ActionFilters;
using Reusable.Utilities.NLog.LayoutRenderers;
using IHostingEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;

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

        public void ConfigureServices(IServiceCollection services)
        {
            SmartPropertiesLayoutRenderer.Register();


            services.AddSingleton<ILoggerFactory>
            (
                new LoggerFactory()
                    .AttachObject("Environment", HostingEnvironment.EnvironmentName)
                    .AttachObject("Product", "Mailr")
                    .AttachScope()
                    .AttachSnapshot()
                    .Attach<Timestamp<DateTimeUtc>>()
                    .AttachElapsedMilliseconds()
                    .AddObserver<NLogRx>()
            );

            services
                .AddMvc()
                .AddExtensions();

            services.AddScoped<ICssProvider, CssProvider>();
            services.Configure<RazorViewEngineOptions>(options =>
            {
                const string prefix = "src";

                options
                    .ViewLocationExpanders
                    .Add(new RelativeViewLocationExpander(prefix));
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

            services.AddScoped<ValidateModel>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseSemanticLogger(config =>
            {
                //config.MapProduct = _ => "Master_SemLog3";
                config.GetCorrelationContext = context =>
                {
                    var product = context.Request.Headers["X-Product"].ElementAtOrDefault(0);
                    var environment = context.Request.Headers["X-Environment"].ElementAtOrDefault(0);

                    return
                             string.IsNullOrWhiteSpace(product) || string.IsNullOrWhiteSpace(environment)
                              ? null
                             //: new Dictionary<string, string> { [name] = value };
                             : new { Product = product, Environment = environment };

                    //return correlationContext is null ? null : new { Header = correlationContext };
                };
            });

            if (env.IsDevelopment())
            {
                app.UseBrowserLink();
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            //app.UseWhen(httpContext => !httpContext.Request.Method.In(new[] { "GET" }, StringComparer.OrdinalIgnoreCase), UseHeaderValidator());

            app.UseMailer();
            app.UseStaticFiles();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
                routes.MapRoute(
                    name: ExtensionType.External.ToString(),
                    template: "{extension}/wwwroot/css/{controller}/{action}.css");
                routes.MapRoute(
                    name: ExtensionType.Internal.ToString(),
                    template: "wwwroot/css/{extension}/{controller}/{action}.css");
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
        public static string Themes = nameof(Themes);
    }
}
