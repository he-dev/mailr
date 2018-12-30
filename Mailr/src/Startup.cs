using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Custom;
using JetBrains.Annotations;
using Mailr.Extensions;
using Mailr.Extensions.Utilities.Mvc.Filters;
using Mailr.Helpers;
using Mailr.Http;
using Mailr.Middleware;
using Mailr.Mvc;
using Mailr.Mvc.Razor.ViewLocationExpanders;
using Mailr.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Reusable;
using Reusable.OmniLog;
using Reusable.OmniLog.Attachements;
using Reusable.OmniLog.SemanticExtensions;
using Reusable.sdk.Mail;
using Reusable.sdk.Outlook;
using Reusable.sdk.Smtp;
using Reusable.Utilities.AspNetCore.ActionFilters;
using Reusable.Utilities.NLog.LayoutRenderers;
using IHostingEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;

[assembly: AspMvcPartialViewLocationFormat("/src/Views/Shared/{0}.cshtml")]
[assembly: AspMvcViewLocationFormat("/src/Views/{1}/{0}.cshtml")]
[assembly: AspMvcViewLocationFormat("/src/Views/Emails/{1}/{0}.cshtml")]
//[assembly: AspMvcViewLocationFormat("/src/Views/Shared/{0}.cshtml")]

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
                    .AttachObject("Product", "Mailr-v3.0.0")
                    .AttachScope()
                    .AttachSnapshot()
                    .Attach<Timestamp<DateTimeUtc>>()
                    .AttachElapsedMilliseconds()
                    .AddObserver<NLogRx>()
            );

            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

            services
                .AddMvc()
                .AddExtensions();

            services.AddApiVersioning(options =>
            {
                options.ApiVersionReader = new HeaderApiVersionReader("Api-Version");
                options.ReportApiVersions = true;
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.DefaultApiVersion = new ApiVersion(1, 0);
            });

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
            services.AddScoped<SendEmail>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            //app.UseMiddleware<LogScopeMiddleware>();
            app.UseSemanticLogger(config =>
            {
                config.ConfigureScope = (scope, context) => scope.AttachUserCorrelationId(context).AttachUserAgent(context);
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

            app.UseEmail();
            app.UseStaticFiles();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
                routes.MapRoute(
                    name: ControllerType.External.ToString(),
                    template: "{extension}/wwwroot/css/{controller}/{action}.css");
                routes.MapRoute(
                    name: ControllerType.Internal.ToString(),
                    template: "wwwroot/css/{extension}/{controller}/{action}.css");
                routes.MapRoute(
                    name: RouteNames.Themes,
                    template: "wwwroot/css/themes/{name}.css");
            });
        }
    }

    internal class RouteNames
    {
        public static string Themes = nameof(Themes);
    }
}
