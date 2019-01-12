using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Custom;
using System.Reflection;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Mailr.Data;
using Mailr.Mvc.Razor.ViewLocationExpanders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Versioning;
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
            var hostingEnvironment = serviceProvider.GetService<IHostingEnvironment>();
            var logger = serviceProvider.GetService<ILoggerFactory>().CreateLogger<Startup>();

            var extensibility = serviceProvider.Extensibility();

            var extensionDirectories =
            (
                hostingEnvironment.IsDevelopmentExt()
                    ? EnumerateExtensionProjectDirectories(serviceProvider)
                    : EnumerateExtensionInstallationDirectories(serviceProvider)
            ).ToList();

            var extensionDirectoriesWithoutRoot = extensionDirectories.Skip(1);

            mvc
                .ConfigureApplicationPartManager(apm =>
                {
                    var binDirectory = extensibility.Bin;

                    // Skip the first directory which is the root and does not contain any extensions.
                    foreach (var extensionDirectory in extensionDirectoriesWithoutRoot)
                    {
                        // Mailr.Extensions.Example
                        var extensionName = Path.GetFileName(extensionDirectory) + ".dll";

                        // Extension assemblies are located in the {Extensibility:Ext}: ..\ext\Foo\bin\Foo.dll
                        var extensionFullName = Path.Combine
                        (
                            extensionDirectory,
                            binDirectory,
                            extensionName
                        );

                        if (TryLoadAssembly(serviceProvider, extensionFullName, out var extensionAssembly))
                        {
                            apm.ApplicationParts.Add(new AssemblyPart(extensionAssembly));                           
                        }
                    }
                });

            ConfigureAssemblyResolve(serviceProvider, extensionDirectoriesWithoutRoot);

            var staticFileProviders =
                extensionDirectories
                    .Select(path => new PhysicalFileProvider(path))
                    .ToList();

            // ContentRootFileProvider is the default one and is always available.
            var fileProvider = new CompositeFileProvider(staticFileProviders.Prepend(hostingEnvironment.ContentRootFileProvider));

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
            var extensibility = serviceProvider.Extensibility();
            var hostingEnvironment = serviceProvider.GetService<IHostingEnvironment>();

            // Razor view engine requires this path too.
            var extRootPath = Path.Combine(hostingEnvironment.ContentRootPath, extensibility.Ext);

            return
                Directory.Exists(extRootPath)
                    ? Directory.GetDirectories(extRootPath).Prepend(extRootPath)
                    : Enumerable.Empty<string>();
        }

        private static IEnumerable<string> EnumerateExtensionProjectDirectories(IServiceProvider serviceProvider)
        {
            var extensibility = serviceProvider.Extensibility();

            // Extension development requires them to be referenced as projects.

            // This path is required to find static files inside the wwwroot directory that is used by the css-provider.
            //var solutionExtensions = configuration["Extensibility:Development:SolutionDirectory"];
            //var projectNames = configuration.GetSection("Extensibility:Development:ProjectNames").GetChildren().AsEnumerable().Select(x => x.Value);
            //return projectNames.Prepend(solutionExtensions).Select(projectName => Path.Combine(solutionExtensions, projectName));

            return
                from extension in extensibility.Development.Extensions
                from path in extension.Projects.Prepend(extension.SolutionDirectory).Select(projectName => Path.Combine(extension.SolutionDirectory, projectName))
                select path;
        }

        private static void ConfigureAssemblyResolve(IServiceProvider serviceProvider, IEnumerable<string> extensionDirectories)
        {
            var extensibility = serviceProvider.Extensibility();
            var hostingEnvironment = serviceProvider.GetService<IHostingEnvironment>();
            var logger = serviceProvider.GetService<ILoggerFactory>().CreateLogger<Startup>();

            var exeDirectory = Path.GetDirectoryName(typeof(Program).Assembly.Location);
            var extRootPath = Path.Combine(hostingEnvironment.ContentRootPath, extensibility.Ext);
            var binDirectoryName = extensibility.Bin;

            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
            {
                // Extract dependency name from the full assembly name:
                // FooPlugin.FooClass, Version = 1.0.0.0, Culture = neutral, PublicKeyToken = null
                var dependencyName = e.Name.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).First() + ".dll";

                logger.Log(Abstraction.Layer.Infrastructure().Meta(new { ResolveAssembly = new { Name = dependencyName, RequestingAssembly = e.RequestingAssembly?.GetName().Name } }));

                var dependencyFullName = Path.Combine
                (
                    exeDirectory,
                    dependencyName
                );

                if (TryLoadAssembly(serviceProvider, dependencyFullName, out var assembly))
                {
                    return assembly;
                }

                // Now try extension directories
                // C:\..\ext\Foo\bin\Bar.dll
                foreach (var directory in extensionDirectories)
                {
                    dependencyFullName = Path.Combine
                    (
                        directory,
                        binDirectoryName,
                        dependencyName
                    );

                    if (TryLoadAssembly(serviceProvider, dependencyFullName, out assembly))
                    {
                        return assembly;
                    }
                }

                return null;
            };
        }

        private static bool TryLoadAssembly(IServiceProvider serviceProvider, string fileName, out Assembly assembly)
        {
            var logger = serviceProvider.GetService<ILoggerFactory>().CreateLogger<Startup>();

            try
            {
                if (File.Exists(fileName))
                {
                    assembly = Assembly.LoadFile(fileName);
                    logger.Log(Abstraction.Layer.Infrastructure().Meta(new { LoadedAssembly = fileName }));
                    return true;
                }
            }
            catch (Exception ex)
            {
                logger.Log(Abstraction.Layer.Infrastructure().Routine(nameof(TryLoadAssembly)).Faulted(), ex);

                if (ex is ReflectionTypeLoadException inner)
                {
                    foreach (var loaderException in inner.LoaderExceptions)
                    {
                        logger.Log(Abstraction.Layer.Infrastructure().Routine(nameof(TryLoadAssembly)).Faulted(), nameof(ReflectionTypeLoadException), loaderException);
                    }
                }
            }

            assembly = default;
            return false;
        }
    }

    public static class HostingEnvironmentExtensions
    {
        public static bool IsDevelopmentExt(this IHostingEnvironment hostingEnvironment)
        {
            return hostingEnvironment.IsEnvironment("DevelopmentExt");
        }
    }

    internal static class ServiceProviderExtensions
    {
        public static Extensibility Extensibility(this IServiceProvider serviceProvider)
        {
            return
                serviceProvider
                    .GetService<IConfiguration>()
                    .GetSection(nameof(Extensibility))
                    .Get<Extensibility>();
        }
    }

}

namespace Mailr.Data
{
    public class Extensibility
    {
        public string Ext { get; set; }

        public string Bin { get; set; }

        public Development Development { get; set; }
    }

    public class Development
    {
        public IEnumerable<string> Bins { get; set; }

        public IEnumerable<Extension> Extensions { get; set; }
    }

    public class Extension
    {
        public string SolutionDirectory { get; set; }

        public IEnumerable<string> Projects { get; set; }
    }

    //public class Project
    //{
    //    public string Name { get; set; }

    //    public string Bin { get; set; }
    //}
}