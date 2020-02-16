using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Autofac;
using JetBrains.Annotations;
using Mailr.Extensions;
using Mailr.Extensions.Helpers;
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Reusable;
using Reusable.Beaver;
using Reusable.Beaver.Policies;
using Reusable.OmniLog;
using Reusable.OmniLog.Abstractions;
using Reusable.OmniLog.SemanticExtensions;
using Reusable.OmniLog.SemanticExtensions.AspNetCore;
using Reusable.OmniLog.SemanticExtensions.AspNetCore.Extensions;
using Reusable.OmniLog.SemanticExtensions.AspNetCore.Mvc.Filters;
using Reusable.OmniLog.Services;
using Reusable.Translucent;
using Reusable.Translucent.Controllers;
using Reusable.Utilities.AspNetCore.ActionFilters;
using Reusable.Utilities.AspNetCore.DependencyInjection;
using Reusable.Utilities.Autofac;
using Reusable.Utilities.NLog.LayoutRenderers;
using Features = Mailr.Extensions.Features;
using IApplicationLifetime = Microsoft.AspNetCore.Hosting.IApplicationLifetime;
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

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            SmartPropertiesLayoutRenderer.Register();

            services.AddOmniLog(loggerFactory =>
                loggerFactory
                    .UseService
                    (
                        new Constant("Environment", HostingEnvironment.EnvironmentName),
                        new Constant("Product", $"{ProgramInfo.Name}-v{ProgramInfo.Version}"),
                        new Timestamp<DateTimeUtc>()
                    )
                    .UseStopwatch()
                    .UseDelegate()
                    .UseScope()
                    .UseBuilder() //n => n.Names.Add(nameof(Abstraction)))
                    .UseDestructure()
                    //.UseMapper(MapperNode.Mapping.For())
                    .UseSerializer()
                    .UsePropertyMapper
                    (
                        (LogProperty.Names.Scope, "Scope"),
                        (LogProperty.Names.SnapshotName, "Identifier"),
                        (LogProperty.Names.Snapshot, "Snapshot")
                    )
                    .UseFallback((LogProperty.Names.Level, LogLevel.Information))
                    //.UseBuffer()
                    .UseEcho(new NLogRx())
            );

            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

            services
                .AddMvc()
                .AddExtensions()
                ;

            services.Configure<RazorViewEngineOptions>(options => { options.AllowRecompilingViewsOnFileChange = true; });

            services.AddApiVersioning(options =>
            {
                //options.ApiVersionReader = new HeaderApiVersionReader("Api-Version");
                options.ReportApiVersions = true;
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.DefaultApiVersion = new ApiVersion(1, 0);
            });

            services.AddRelativeViewLocationExpander();
            services.AddSingleton<IHostedService, WorkItemQueueService>();
            services.AddSingleton<IWorkItemQueue, WorkItemQueue>();
            services.AddScoped<ICssProvider, CssProvider>();
            services.AddScoped<ValidateModel>();
            services.AddScoped<SendEmail>();
            services.AddScoped<LogResponseBody>();

            //var runtimeId = RuntimeEnvironment.GetRuntimeIdentifier();
            //var assemblyNames = DependencyContext.Default.GetRuntimeAssemblyNames(runtimeId);
            //var extensionAssemblyNames = assemblyNames.Where(a => a.Name.StartsWith("Mailr.Extensions."));
            //var extensionAssemblies = 
            //    extensionAssemblyNames
            //        .SelectMany(a => AssemblyLoadContext.Default.LoadFromAssemblyName(a).DefinedTypes)
            //        .Where(t => typeof(Controller).IsAssignableFrom(t))
            //        .ToList();

            var wwwrootFileProviders =
                services
                    .EnumerateExtensionDirectories()
                    .Select(n => new PhysicalFileProvider(Path.Combine(HostingEnvironment.ContentRootPath, n)))
                    .Prepend(HostingEnvironment.ContentRootFileProvider);

            services.AddSingleton<IFileProvider>(new CompositeFileProvider(wwwrootFileProviders));

            return
                AutofacLifetimeScopeBuilder
                    .From(services)
                    .Configure(builder =>
                    {
                        builder
                            .Register(_ => new FeatureCollection
                            {
                                { new Feature(Features.SendEmail.ToString(), FeaturePolicy.AlwaysOn) },
                                { new Feature.Telemetry(Features.SendEmail.ToString(), FeaturePolicy.AlwaysOn) }
                            })
                            .As<IFeatureCollection>()
                            .InstancePerLifetimeScope();

                        builder
                            .RegisterType<FeatureToggle>()
                            .WithParameter(new TypedParameter(typeof(IFeaturePolicy), FeaturePolicy.AlwaysOff))
                            .As<IFeatureToggle>()
                            .InstancePerLifetimeScope();

                        builder
                            .RegisterType<FeatureController>()
                            .As<IFeatureController>()
                            .InstancePerLifetimeScope();

                        builder
                            .RegisterDecorator<FeatureControllerTelemetry, IFeatureController>();
                        
                        builder
                            .RegisterType<AutofacServiceProvider>()
                            .As<IServiceProvider>();

                        builder
                            .Register(ctx => Resource.Builder().UseController(new SmtpController()).Build())
                            .As<IResource>();
                    })
                    .ToServiceProvider();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime appLifetime, ILoggerFactory loggerFactory)
        {
            //app.UseMiddleware<LogScopeMiddleware>();
            app.UseOmniLog();

            var startupLogger = loggerFactory.CreateLogger<Startup>();

            appLifetime.ApplicationStarted.Register(() => { startupLogger.Log(Abstraction.Layer.Service().Routine("Start").Completed(), l => l.Message("Here's Mailr!")); });

            appLifetime.ApplicationStopped.Register(() => { startupLogger.Log(Abstraction.Layer.Service().Routine("Stop").Completed(), l => l.Message("Good bye!")); });

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

            //var staticFileProviders = app.EnumerateExtensionDirectories().Select(path => new PhysicalFileProvider(path)).Prepend(env.ContentRootFileProvider);
            app.UseStaticFiles();
            //(new StaticFileOptions
            //{
            //    FileProvider = new CompositeFileProvider(staticFileProviders.ToList()),
            //});

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
                routes.MapRoute(
                    name: RouteNames.Css.Global,
                    template: "wwwroot/css/{theme}.css");
                routes.MapRoute(
                    name: RouteNames.Css.Extension,
                    template: "wwwroot/css/{area}/{controller}/{action}/{theme}.css");
            });
        }
    }


    internal static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddRelativeViewLocationExpander(this IServiceCollection services, string prefix = "src")
        {
            return services.Configure<RazorViewEngineOptions>(options =>
            {
                options
                    .ViewLocationExpanders
                    .Add(new RelativeViewLocationExpander(prefix));
            });
        }
    }
}