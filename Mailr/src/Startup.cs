using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Custom;
using System.Reflection;
using JetBrains.Annotations;
using Mailr.Helpers;
using Mailr.Middleware;
using Mailr.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
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
using Reusable.Utilities.AspNetCore;
using Reusable.Utilities.ThirdParty.NLog.LayoutRenderers;

[assembly: AspMvcViewLocationFormat("/src/Views/{1}/{0}.cshtml")]
[assembly: AspMvcViewLocationFormat("/src/Views/Emails/{1}/{0}.cshtml")]
[assembly: AspMvcViewLocationFormat("/src/Views/Shared/{0}.cshtml")]

namespace Mailr
{
    public class Startup
    {
        private IConfiguration Configuration { get; }

        private IHostingEnvironment HostingEnvironment { get; }

        public Startup(IConfiguration configuration, IHostingEnvironment hostingEnvironment)
        {
            Configuration = configuration;
            HostingEnvironment = hostingEnvironment;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            SmartPropertiesLayoutRenderer.Register();

            //services.AddSingleton(Configuration);
            services.AddSingleton(LoggerFactorySetup.SetupLoggerFactory(HostingEnvironment.EnvironmentName, "MailrAPI", new[] { NLogRx.Create() }));

            services
                .AddMvc()
                .AddPlugins();

            services.AddSingleton(HostingEnvironment.ContentRootFileProvider);
            services.AddScoped<ICssProvider, CssProvider>();
            services.Configure<RazorViewEngineOptions>(options =>
            {
                options.ViewLocationExpanders.Add(new RelativeViewLocationExpander());
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

            if (env.IsDevelopmentAny())
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
                routes.MapRoute(
                    name: RouteNames.Emails,
                    template: "wwwroot/css/emails/{controller}/{action}.css");
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
        public static string Themes = nameof(Themes);
    }

    public class RelativeViewLocationExpander : IViewLocationExpander
    {
        public void PopulateValues(ViewLocationExpanderContext context)
        {
            context.Values[nameof(RelativeViewLocationExpander)] = nameof(RelativeViewLocationExpander);
        }

        public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
        {
            foreach (var viewLocation in viewLocations)
            {
                yield return $"/src{viewLocation}";
            }

            yield return $"/src/Views/Emails/{{1}}/{{0}}{RazorViewEngine.ViewExtension}";
        }
    }

    public static class MvcBuilderPluginExtensions
    {
        // Adds plugins located in \{Root}\Plugin\{Binary}\Plugin.dll
        // Example: \ext\Plugin\bin\Plugin.dll    
        public static IMvcBuilder AddPlugins(this IMvcBuilder mvc)
        {
            var serviceProvider = mvc.Services.BuildServiceProvider();
            var configuration = serviceProvider.GetService<IConfiguration>();
            var hostingEnvironment = serviceProvider.GetService<IHostingEnvironment>();
            var logger = serviceProvider.GetService<ILoggerFactory>().CreateLogger<Startup>();

            var pluginsRootPath = Path.Combine(hostingEnvironment.ContentRootPath, configuration["PluginDirectory:Root"]);
            var pluginAssemblies = GetPluginAssemblies(pluginsRootPath, configuration["PluginDirectory:Binary"]).ToList();

            logger.Log(Abstraction.Layer.Infrastructure().Data().Variable(new { pluginAssemblies = pluginAssemblies.Select(x => x.FullName) }));

            mvc
                .ConfigureApplicationPartManager(apm =>
                {
                    foreach (var pluginAssembly in pluginAssemblies)
                    {
                        logger.Log(Abstraction.Layer.Infrastructure().Data().Object(new { pluginAssembly = new { pluginAssembly.FullName } }));
                        apm.ApplicationParts.Add(new AssemblyPart(pluginAssembly));
                    }
                });

            mvc
                .Services
                .ConfigureRazorViewEngine(hostingEnvironment, pluginAssemblies, pluginsRootPath);

            ConfigureAssemblyResolve(logger, pluginsRootPath, configuration["PluginDirectory:Binary"]);

            return mvc;
        }

        private static IEnumerable<Assembly> GetPluginAssemblies(string pluginsRootPath, string binDirectoryName)
        {
            if (!Directory.Exists(pluginsRootPath))
            {
                yield break;
            }

            var pluginDirectories = Directory.GetDirectories(pluginsRootPath);
            foreach (var pluginDirectory in pluginDirectories)
            {
                // C:\..\ext\Plugin\bin\Plugin.dll
                var pluginFullName =
                    Path.Combine(
                        pluginDirectory,
                        binDirectoryName,
                        $"{Path.GetFileName(pluginDirectory)}.dll"
                    );

                if (File.Exists(pluginFullName))
                {
                    yield return Assembly.LoadFile(pluginFullName);
                }
            }
        }

        private static void ConfigureAssemblyResolve(ILogger logger, string pluginsRootPath, string binDirectoryName)
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
            {
                // Extract dependency name from the full assembly name:
                // FooPlugin.FooClass, Version = 1.0.0.0, Culture = neutral, PublicKeyToken = null
                var pluginDependencyName = e.Name.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).First();

                // C:\..\ext\Plugin\bin\PluginDependency.dll
                var pluginDependencyFullName =
                    Path.Combine(
                        pluginsRootPath,
                        pluginDependencyName,
                        binDirectoryName,
                        $"{pluginDependencyName}.dll"
                    );

                logger.Log(Abstraction.Layer.Infrastructure().Data().Variable(new { pluginDependencyFullName }));

                return
                    File.Exists(pluginDependencyFullName)
                        ? Assembly.LoadFile(pluginDependencyFullName)
                        : null;
            };
        }

        // Adds plugin directory to Razor view engine so that it can resolve plugin's views e.g. \ext\Plugin
        private static void ConfigureRazorViewEngine(this IServiceCollection services, IHostingEnvironment hostingEnvironment, IEnumerable<Assembly> pluginAssemblies, string pluginsRootPath)
        {
            services.Configure<RazorViewEngineOptions>(options =>
            {
                foreach (var pluginAssembly in pluginAssemblies)
                {
                    var pluginRootPath =
                        Path.Combine(
                            pluginsRootPath,
                            pluginAssembly.GetName().Name
                        );

                    options
                        .FileProviders
                        .Add(new PhysicalFileProvider(pluginRootPath));
                }

                // Extension development does not use plugins so we have to look for it in the current directory parent 
                // because the service is "installed" as a submodule which is a subdirectory.
                if (hostingEnvironment.IsDevelopment("Extension"))
                {
                    // ContentRootPath is the path of the *.csproj, we have to go back two levels to reach the extension directory.
                    var extensionDirectory = new DirectoryInfo(hostingEnvironment.ContentRootPath).Parent?.Parent;

                    if (extensionDirectory is null)
                    {
                        throw new DirectoryNotFoundException("Could not find extension directory.");
                    }

                    options
                        .FileProviders
                        .Add(new PhysicalFileProvider(Path.Combine(extensionDirectory.FullName, extensionDirectory.Name)));
                }
            });
        }
    }
}
