using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Custom;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using JetBrains.Annotations;
using Mailr.Extensions;
using Mailr.Extensions.Helpers;
using Mailr.Extensions.Mvc.TagHelpers;
using Mailr.Extensions.Utilities.Mvc.Filters;
using Mailr.Helpers;
using Mailr.Http;
using Mailr.Middleware;
using Mailr.Mvc;
using Mailr.Mvc.Razor.ViewLocationExpanders;
using Mailr.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Reusable;
using Reusable.IOnymous;
using Reusable.OmniLog;
using Reusable.OmniLog.Abstractions;
using Reusable.OmniLog.Attachments;
using Reusable.OmniLog.SemanticExtensions;
using Reusable.Utilities.AspNetCore.ActionFilters;
using Reusable.Utilities.NLog.LayoutRenderers;
using IHostingEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;
using PhysicalFileProvider = Microsoft.Extensions.FileProviders.PhysicalFileProvider;

[assembly: AspMvcMasterLocationFormat("~/src/Views/{1}/{0}.cshtml")]
[assembly: AspMvcViewLocationFormat("~/src/Views/{1}/{0}.cshtml")]
[assembly: AspMvcPartialViewLocationFormat("~/src/Views/Shared/{0}.cshtml")]

[assembly: InternalsVisibleTo("Mailr.Tests")]

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
                LoggerFactory
                    .Empty
                    .AttachObject("Environment", HostingEnvironment.EnvironmentName)
                    .AttachObject("Product", $"{ProgramInfo.Name}-v{ProgramInfo.Version}")
                    .AttachScope()
                    .AttachSnapshot()
                    .Attach<Timestamp<DateTimeUtc>>()
                    .AttachElapsedMilliseconds()
                    .AddObserver<NLogRx>()
            );

            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

            services
                .AddMvc()
                //.AddExtensions()
                ;

            services.Configure<RazorViewEngineOptions>(options => { options.AllowRecompilingViewsOnFileChange = true; });

            services.AddApiVersioning(options =>
            {
                //options.ApiVersionReader = new HeaderApiVersionReader("Api-Version");
                options.ReportApiVersions = true;
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.DefaultApiVersion = new ApiVersion(1, 0);
            });

            services.AddScoped<ICssProvider, CssProvider>();
            services.AddRelativeViewLocationExpander();


            var mailProviderRegistrations = new Dictionary<SoftString, Action>
            {
                [nameof(SmtpProvider)] = () => services.AddSingleton<IResourceProvider, SmtpProvider>()
                //[nameof(OutlookProvider)] = () => services.AddSingleton<IResourceProvider, OutlookProvider>(),
            };

            var mailProvider = Configuration["mailProvider"];
            if (mailProviderRegistrations.TryGetValue(mailProvider, out var register))
            {
                register();
            }
            else
            {
                throw new ArgumentOutOfRangeException($"Invalid mail-provider: {mailProvider}. Expected [{mailProviderRegistrations.Keys.Join(", ")}].");
            }

            services.AddSingleton<IHostedService, WorkItemQueueService>();
            services.AddSingleton<IWorkItemQueue, WorkItemQueue>();

            services.AddScoped<ValidateModel>();
            services.AddScoped<SendEmail>();
            //services.AddScoped<ImportCssTagHelper>();

            //var runtimeId = RuntimeEnvironment.GetRuntimeIdentifier();
            //var assemblyNames = DependencyContext.Default.GetRuntimeAssemblyNames(runtimeId);
            //var extensionAssemblyNames = assemblyNames.Where(a => a.Name.StartsWith("Mailr.Extensions."));
            //var extensionAssemblies = 
            //    extensionAssemblyNames
            //        .SelectMany(a => AssemblyLoadContext.Default.LoadFromAssemblyName(a).DefinedTypes)
            //        .Where(t => typeof(Controller).IsAssignableFrom(t))
            //        .ToList();

            //var currentDirectory =
            //var staticFileProviders =
            //    extensionAssemblyNames
            //        .Select(n => new PhysicalFileProvider(Path.Combine(HostingEnvironment.ContentRootPath, n)))
            //        .ToList();

            if (HostingEnvironment.IsDevelopmentExt())
            {
                var xMailr = XDocument.Load(Path.Combine(HostingEnvironment.ContentRootPath, "Mailr.csproj"));

                var extensionPaths =
                    xMailr
                        .Root
                        .Elements("ItemGroup")
                        .SelectMany(x => x.Elements("ProjectReference"))
                        .Select(x => x.Attribute("Include").Value)
                        .Where(x => Regex.IsMatch(x, @"Mailr\.Extensions\.\w+\.csproj"))
                        .Select(n => Path.GetDirectoryName(n)) // Regex.Replace(n, @"\.csproj$", string.Empty))
                        .Select(n => new PhysicalFileProvider(Path.Combine(HostingEnvironment.ContentRootPath, n)))
                        .Append(HostingEnvironment.ContentRootFileProvider);

                services.AddSingleton<IFileProvider>(new CompositeFileProvider(extensionPaths));
            }
            else
            {
                services.AddSingleton<IFileProvider>(HostingEnvironment.ContentRootFileProvider);
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            //app.UseMiddleware<LogScopeMiddleware>();
            app.UseSemanticLogger(config => { config.ConfigureScope = (scope, context) => scope.AttachUserCorrelationId(context).AttachUserAgent(context); });

            if (env.IsDevelopment() || env.IsDevelopmentExt())
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
                //routes.MapRoute(
                //    name: "areas",
                //    template: "{area}/{controller=Home}/{action=Index}/{id?}");
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
                routes.MapRoute(
                    name: RouteNameFactory.CreateCssRouteName(ControllerType.Internal, false),
                    template: "wwwroot/css/{extension}/{controller}/{action}.css");
                routes.MapRoute(
                    name: RouteNameFactory.CreateCssRouteName(ControllerType.External, false),
                    //template: "{extension}/wwwroot/css/{controller}/{action}.css");
                    template: "wwwroot/css/{extension}/{controller}/{action}.css");
                //routes.MapRoute(
                //    name: RouteNameFactory.CreateCssRouteName(ControllerType.External, true),
                //    template: "{extension}/wwwroot/css/{controller}/{action}-{theme}.css");
                routes.MapRoute(
                    name: RouteNameFactory.CreateCssRouteName(ControllerType.External, true),
                    template: "wwwroot/css/{extension}/{controller}/{action}-{theme}.css");
                routes.MapRoute(
                    name: RouteNames.Themes,
                    template: "wwwroot/css/themes/{theme}.css");
            });
        }
    }

    internal static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddRelativeViewLocationExpander(this IServiceCollection services)
        {
            return services.Configure<RazorViewEngineOptions>(options =>
            {
                const string prefix = "src";

                options
                    .ViewLocationExpanders
                    .Add(new RelativeViewLocationExpander(prefix));
            });
        }
    }
}