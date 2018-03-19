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
        private const string PluginsDirectoryName = "ext";

        private readonly IConfiguration _configuration;

        private readonly IHostingEnvironment _hostingEnvironment;

        public Startup(IConfiguration configuration, IHostingEnvironment hostingEnvironment)
        {
            _configuration = configuration;
            _hostingEnvironment = hostingEnvironment;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            SmartPropertiesLayoutRenderer.Register();

            var loggerFactory = LoggerFactorySetup.SetupLoggerFactory(_hostingEnvironment.EnvironmentName, "MailrAPI", new[] { NLogRx.Create() });
            var logger = loggerFactory.CreateLogger<Startup>();

            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
            {
                var pluginName = e.RequestingAssembly.GetName().Name;

                // Extract dependency name from the full assembly name:
                // PluginTest.HalloWorldHelper, Version = 1.0.0.0, Culture = neutral, PublicKeyToken = null
                var pluginDependencyName = e.Name.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).First();

                var pluginDependencyFullName =
                    Path.Combine(
                        _hostingEnvironment.ContentRootPath,
                        PluginsDirectoryName,
                        pluginDependencyName,
                        $"{pluginDependencyName}.dll"
                    );

                logger.Log(Abstraction.Layer.Infrastructure().Data().Variable(new { pluginDependencyFullName }));

                return
                    File.Exists(pluginDependencyFullName)
                        ? Assembly.LoadFile(pluginDependencyFullName)
                        : null;
            };

            services.AddSingleton(loggerFactory);


            var pluginAssemblies =
                GetPluginAssemblies(_hostingEnvironment)
                    .ToList();

            logger.Log(Abstraction.Layer.Infrastructure().Data().Variable(new { pluginAssemblies = pluginAssemblies.Select(x => x.FullName) }));

            services
                .AddMvc()
                .ConfigureApplicationPartManager(apm =>
                {
                    foreach (var pluginAssembly in pluginAssemblies)
                    {
                        logger.Log(Abstraction.Layer.Infrastructure().Data().Object(new { pluginAssembly = new { pluginAssembly.FullName } }));
                        apm.ApplicationParts.Add(new AssemblyPart(pluginAssembly));
                    }
                });

            //Add the file provider to the Razor view engine
            services.Configure<RazorViewEngineOptions>(options =>
            {
                //foreach (var pluginAssembly in pluginAssemblies)
                //{
                //    //options
                //    //    .FileProviders
                //    //    .Add(new EmbeddedFileProvider(pluginAssembly));

                //    var extensionDirectory =
                //        Path.Combine(
                //            _hostingEnvironment.ContentRootPath,
                //            PluginsDirectoryName,
                //            pluginAssembly.GetName().Name
                //        );

                //    options
                //        .FileProviders
                //        .Add(new PhysicalFileProvider(extensionDirectory));

                //}

                // Extension development does not use plugins so we have to look for it in the current directory parent.
                if (_hostingEnvironment.IsDevelopment("Extension"))
                {
                    // ContentRootPath is the path of the *.csproj, we have to go back two levels to reach the extension directory.
                    var extensionDirectory = new DirectoryInfo(_hostingEnvironment.ContentRootPath).Parent?.Parent;

                    if (extensionDirectory is null)
                    {
                        throw new DirectoryNotFoundException("Could not find extension directory.");
                    }

                    options
                        .FileProviders
                        .Add(new PhysicalFileProvider(Path.Combine(extensionDirectory.FullName, extensionDirectory.Name)));
                }
            });

            services.AddSingleton(_hostingEnvironment.ContentRootFileProvider);
            services.AddScoped<ICssProvider, CssProvider>();
            services.Configure<RazorViewEngineOptions>(options =>
            {
                options.ViewLocationExpanders.Add(new RelativeViewLocationExpander());
            });

            var emailClient = _configuration["emailClient"];
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

        private static IEnumerable<Assembly> GetPluginAssemblies(IHostingEnvironment hostingEnvironment)
        {
            var pluginDirectoryName = Path.Combine(hostingEnvironment.ContentRootPath, PluginsDirectoryName);

            if (!Directory.Exists(pluginDirectoryName))
            {
                yield break;
            }

            var pluginDirectories = Directory.GetDirectories(pluginDirectoryName);
            foreach (var pluginDirectory in pluginDirectories)
            {
                var pluginFullName =
                    Path.Combine(
                        hostingEnvironment.ContentRootPath,
                        pluginDirectory,
                        $"{Path.GetFileName(pluginDirectory)}.dll"
                    );

                if (File.Exists(pluginFullName))
                {
                    yield return Assembly.LoadFile(pluginFullName);
                }
            }
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
                //yield return viewLocation;
                yield return $"/src{viewLocation}";
            }

            //yield return $"/Views/Emails/{{1}}/{{0}}{RazorViewEngine.ViewExtension}";
            yield return $"/src/Views/Emails/{{1}}/{{0}}{RazorViewEngine.ViewExtension}";
        }
    }
}
