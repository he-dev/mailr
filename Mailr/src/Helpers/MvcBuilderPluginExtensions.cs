using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Reusable.OmniLog;
using Reusable.OmniLog.SemanticExtensions;

namespace Mailr.Helpers
{
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

            var pluginsRootPath = Path.Combine(hostingEnvironment.ContentRootPath, configuration["ExtensionDirectory:Root"]);
            var pluginAssemblies = GetPluginAssemblies(pluginsRootPath, configuration["ExtensionDirectory:Binary"]).ToList();

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

            mvc
                .Services
                .AddSingleton<IFileProvider>(
                    new CompositeFileProvider(
                        CreateExtensionFileProviders(
                            hostingEnvironment,
                            pluginAssemblies,
                            pluginsRootPath
                        )
                    )
                );

            ConfigureAssemblyResolve(logger, pluginsRootPath, configuration["ExtensionDirectory:Binary"]);

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
                
                foreach (var directory in GetExtensionDirectories(hostingEnvironment))
                {
                    options
                        .FileProviders
                        .Add(new PhysicalFileProvider(directory));
                }
            });
        }

        private static IEnumerable<IFileProvider> CreateExtensionFileProviders(IHostingEnvironment hostingEnvironment, IEnumerable<Assembly> pluginAssemblies, string pluginsRootPath)
        {
            yield return hostingEnvironment.ContentRootFileProvider;

            foreach (var directory in GetExtensionDirectories(hostingEnvironment))
            {
                yield return new PhysicalFileProvider(directory);
            }
        }

        private static IEnumerable<string> GetExtensionDirectories(IHostingEnvironment hostingEnvironment)
        {
            // Extension development does not use plugins so we have to look for them in the current directory parent 
            // because the service is "installed" as a submodule which is a subdirectory.

            if (hostingEnvironment.IsSubmodule())
            {
                // ContentRootPath is the path of the *.csproj, we have to go back two levels to reach the extension directory.
                var extensionsRootDirectory =
                    new DirectoryInfo(hostingEnvironment.ContentRootPath).Parent?.Parent
                    ?? throw new DirectoryNotFoundException("Could not find extension directory.");

                foreach (var directory in Directory.EnumerateDirectories(extensionsRootDirectory.FullName, "Mailr.Extensions.*"))
                {
                    yield return directory;
                }
            }
        }
    }

    public static class HostingEnvironmentExtensions
    {
        public static bool IsSubmodule(this IHostingEnvironment hostingEnvironment)
        {
            return hostingEnvironment.IsEnvironment("Submodule");
        }
    }
}