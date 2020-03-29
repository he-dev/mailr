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
using Newtonsoft.Json;
using Reusable;
using Reusable.Beaver;
using Reusable.Beaver.Policies;
using Reusable.OmniLog;
using Reusable.OmniLog.Abstractions;
using Reusable.OmniLog.Connectors;
using Reusable.OmniLog.Extensions;
using Reusable.OmniLog.Nodes;
using Reusable.OmniLog.Properties;
using Reusable.OmniLog.Utilities.AspNetCore;
using Reusable.OmniLog.Utilities.AspNetCore.Mvc.Filters;
using Reusable.Translucent;
using Reusable.Translucent.Abstractions;
using Reusable.Translucent.Controllers;
using Reusable.Utilities.AspNetCore.ActionFilters;
using Reusable.Utilities.AspNetCore.DependencyInjection;
using Reusable.Utilities.AspNetCore.Middleware;
using Reusable.Utilities.Autofac;
using Reusable.Utilities.JsonNet;
using Reusable.Utilities.JsonNet.Abstractions;
using Reusable.Utilities.JsonNet.Converters;
using Reusable.Utilities.JsonNet.Services;
using Reusable.Utilities.JsonNet.TypeDictionaries;
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

            services.AddOmniLog(
                LoggerPipelines
                    .Complete
                    .Configure<AttachProperty>(node =>
                    {
                        node.Properties.Add(new Constant("Environment", HostingEnvironment.EnvironmentName));
                        node.Properties.Add(new Constant("Product", $"{ProgramInfo.Name}-v{ProgramInfo.Version}"));
                        node.Properties.Add(new Timestamp<DateTimeUtc>());
                    })
                    .Configure<RenameProperty>(node =>
                    {
                        node.Mappings.Add(Names.Properties.Correlation, "Scope");
                        node.Mappings.Add(Names.Properties.Unit, "Identifier");
                        node.Mappings.Add(Names.Properties.Snapshot, "Snapshot");
                    })
                    .Configure<Echo>(node => { node.Connectors.Add(new NLogConnector()); })
                    .ToLoggerFactory()
            );

            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            services.AddScoped(_ => BuildInTypeDictionary.Default.Add(new CurrentDomainTypeDictionary()));

            services
                .AddMvc()
                .AddJsonOptions(options => { options.SerializerSettings.TypeNameHandling = TypeNameHandling.Auto; })
                .AddExtensions()
                ;
            
            services.Configure<ApiBehaviorOptions>(o =>
            {
                o.InvalidModelStateResponseFactory = actionContext =>
                {
                    return new BadRequestObjectResult(actionContext.ModelState);
                };
            });

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
            //services.AddSingleton<IGetJsonTypes>(new GetJsonTypesInCurrentDomain());

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
                                { Features.SendEmail, FeaturePolicy.AlwaysOn },
                                { Feature.Telemetry.CreateName(Features.SendEmail), FeaturePolicy.AlwaysOn }
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
                            .RegisterDecorator<FeatureTelemetry, IFeatureController>();

                        builder
                            .RegisterType<AutofacServiceProvider>()
                            .As<IServiceProvider>();

                        builder
                            .Register(ctx => new Resource(new IResourceController[] { new SmtpController() }))
                            .As<IResource>();
                    })
                    .ToServiceProvider();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime appLifetime, ILoggerFactory loggerFactory)
        {
            //app.UseMiddleware<LogScopeMiddleware>();
            app.UseOmniLog();
            app.UseMiddleware<NormalizeJsonTypePropertyMiddleware>();

            var startupLogger = loggerFactory.CreateLogger<Startup>();

            //appLifetime.ApplicationStarted.Register(() => { startupLogger.Log(Abstraction.Layer.Service().Routine("Start").Completed(), l => l.Message("Here's Mailr!")); });

            //appLifetime.ApplicationStopped.Register(() => { startupLogger.Log(Abstraction.Layer.Service().Routine("Stop").Completed(), l => l.Message("Good bye!")); });

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