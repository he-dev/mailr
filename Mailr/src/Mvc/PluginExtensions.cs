using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Custom;
using System.Reflection;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Mailr.Mvc.Razor.ViewLocationExpanders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Reusable.OmniLog;
using Reusable.OmniLog.SemanticExtensions;

namespace Mailr.Mvc
{
    public static class PluginExtensions
    {
        // Adds plugins located in \{Ext}\Foo\{Bin}\Foo.dll
        // Example: \ext\Foo\bin\Foo.dll    
        public static IMvcBuilder AddExtensions(this IMvcBuilder mvc)
        {
            var serviceProvider = mvc.Services.BuildServiceProvider();
            var configuration = serviceProvider.GetService<IConfiguration>();
            var hostingEnvironment = serviceProvider.GetService<IHostingEnvironment>();
            var logger = serviceProvider.GetService<ILoggerFactory>().CreateLogger<Startup>();

            //logger.Log(Abstraction.Layer.Infrastructure().Variable(new { pluginAssemblies = extAssemblies.Select(x => x.FullName) }));

            var extensionDirectories =
                (hostingEnvironment.IsDevelopmentExt()
                    ? EnumerateExtensionProjectDirectories(serviceProvider)
                    : EnumerateExtensionInstallationDirectories(serviceProvider)).ToList();

            mvc
                .ConfigureApplicationPartManager(apm =>
                {
                    foreach (var extensionDirectory in extensionDirectories)
                    {
                        if (TryLoadExtensionAssembly(serviceProvider, extensionDirectory, out var extensionAssembly))
                        {
                            logger.Log(Abstraction.Layer.Infrastructure().Meta(new { extensionAssembly = new { extensionAssembly.FullName } }));
                            apm.ApplicationParts.Add(new AssemblyPart(extensionAssembly));
                        }
                    }
                });

            if (hostingEnvironment.IsProduction() || hostingEnvironment.IsDevelopment())
            {
                ConfigureAssemblyResolve(serviceProvider);
            }

            var resouceFileProviders =
                extensionDirectories
                    .Select(path => new PhysicalFileProvider(path))
                    .ToList();

            // ContentRootFileProvider is the default one and is always available.
            var fileProvider = new CompositeFileProvider(new[] { hostingEnvironment.ContentRootFileProvider }.Concat(resouceFileProviders));

            mvc
                .Services
                .AddSingleton<IFileProvider>(fileProvider)
                .Configure<RazorViewEngineOptions>(options =>
                {
                    options
                        .FileProviders
                        .Add(fileProvider);
                });

            return mvc;
        }

        private static IEnumerable<string> EnumerateExtensionInstallationDirectories(IServiceProvider serviceProvider)
        {
            var configuration = serviceProvider.GetService<IConfiguration>();
            var hostingEnvironment = serviceProvider.GetService<IHostingEnvironment>();

            // Razor view engine requires this path too.
            var extRootPath = Path.Combine(hostingEnvironment.ContentRootPath, configuration["Extensibility:Ext"]);

            return
                Directory.Exists(extRootPath)
                    ? Directory.GetDirectories(extRootPath).Prepend(extRootPath)
                    : Enumerable.Empty<string>();
        }

        private static IEnumerable<string> EnumerateExtensionProjectDirectories(IServiceProvider serviceProvider)
        {
            var configuration = serviceProvider.GetService<IConfiguration>();
            var hostingEnvironment = serviceProvider.GetService<IHostingEnvironment>();

            // Extension development requires them to be referenced as projects.

            // This path is required to find static files by css-provider.
            var solutionExtensions = configuration["Extensibility:Development:SolutionDirectory"];
            var projectNames = configuration.GetSection("Extensibility:Development:ProjectNames").GetChildren().AsEnumerable().Select(x => x.Value);

            //foreach (var directory in Directory.EnumerateDirectories(extensionsRootDirectory.FullName, "Mailr.Extensions.*"))
            foreach (var directory in projectNames.Prepend(solutionExtensions).Select(projectName => Path.Combine(solutionExtensions, projectName)))
            {
                yield return directory;
            }
        }

        private static bool TryLoadExtensionAssembly(IServiceProvider serviceProvider, string extensionDirectory, out Assembly assembly)
        {
            var hostingEnvironment = serviceProvider.GetService<IHostingEnvironment>();
            var configuration = serviceProvider.GetService<IConfiguration>();
            var logger = serviceProvider.GetService<ILoggerFactory>().CreateLogger<Startup>();

            var binDirectoryName = configuration["Extensibility:Bin"];

            var extensionName = Path.GetFileName(extensionDirectory);

            if (hostingEnvironment.IsDevelopmentExt())
            {
                extensionDirectory = hostingEnvironment.ContentRootPath;
            }

            // Extension assemblies are located in the {Extensibility:Ext}: ..\ext\Foo\bin\Foo.dll
            var extensionDllName =
                Path.Combine(
                    extensionDirectory,
                    binDirectoryName,
                    $"{extensionName}.dll"
                );

            try
            {
                if (File.Exists(extensionDllName))
                {
                    assembly = Assembly.LoadFile(extensionDllName);
                    return true;
                }
            }
            catch (Exception ex)
            {
                logger.Log(Abstraction.Layer.Infrastructure().Routine(nameof(TryLoadExtensionAssembly)).Faulted(), ex);
            }

            assembly = default;
            return false;
        }

        private static void ConfigureAssemblyResolve(IServiceProvider serviceProvider)
        {
            var configuration = serviceProvider.GetService<IConfiguration>();
            var hostingEnvironment = serviceProvider.GetService<IHostingEnvironment>();
            var logger = serviceProvider.GetService<ILoggerFactory>().CreateLogger<Startup>();

            var extRootPath = Path.Combine(hostingEnvironment.ContentRootPath, configuration["Extensibility:Ext"]);
            var binDirectoryName = configuration["Extensibility:Bin"];

            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
            {
                // Extract dependency name from the full assembly name:
                // FooPlugin.FooClass, Version = 1.0.0.0, Culture = neutral, PublicKeyToken = null
                var extDependencyName = e.Name.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).First();

                // C:\..\ext\Foo\bin\FooDependency.dll
                var extDependencyFullName =
                    Path.Combine(
                        extRootPath,
                        extDependencyName,
                        binDirectoryName,
                        $"{extDependencyName}.dll"
                    );

                logger.Log(Abstraction.Layer.Infrastructure().Variable(new { pluginDependencyFullName = extDependencyFullName }));

                return
                    File.Exists(extDependencyFullName)
                        ? Assembly.LoadFile(extDependencyFullName)
                        : null;
            };
        }
    }

    public static class HostingEnvironmentExtensions
    {
        public static bool IsDevelopmentExt(this IHostingEnvironment hostingEnvironment)
        {
            return hostingEnvironment.IsEnvironment("DevelopmentExt");
        }
    }
}