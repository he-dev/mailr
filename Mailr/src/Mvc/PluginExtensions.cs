using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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

            mvc
                .ConfigureApplicationPartManager(apm =>
                {
                    //apm.ApplicationParts.Add(new AssemblyPart(typeof(Mailr.Extensions.Gunter.Controllers.RunTestController).Assembly));
                    //return;
                    var extensionDirectories =
                        hostingEnvironment.IsDevelopmentExt()
                            ? GetDevelopmentExtensionDirectories(serviceProvider)
                            : GetExtensionDirectories(serviceProvider);
                    //if (!configuration.GetSection("Extensibility:Enabled").Get<bool>())
                    //{
                    //    return;
                    //}
                    foreach (var extensionDirectory in extensionDirectories)
                    {
                        try
                        {
                            var extensionAssembly = LoadExtensionAssembly(serviceProvider, extensionDirectory);
                            logger.Log(Abstraction.Layer.Infrastructure().Meta(new { extensionAssembly = new { extensionAssembly.FullName } }));
                            apm.ApplicationParts.Add(new AssemblyPart(extensionAssembly));
                        }
                        catch (FileNotFoundException ex)
                        {
                            logger.Log(Abstraction.Layer.Infrastructure().Routine(nameof(LoadExtensionAssembly)).Faulted(), ex);
                        }
                        catch (ReflectionTypeLoadException ex)
                        {
                            logger.Log(Abstraction.Layer.Infrastructure().Routine(nameof(LoadExtensionAssembly)).Faulted(), ex);
                        }
                        catch (Exception ex)
                        {
                            logger.Log(Abstraction.Layer.Infrastructure().Routine($"{nameof(AssemblyPart)}.ctor").Faulted(), ex);
                        }
                    }
                });

            ConfigureAssemblyResolve(serviceProvider);


            var resouceFileProviders =
                EnumerateResourceDirectories(serviceProvider)
                    .Select(path => new PhysicalFileProvider(path))
                    .ToList();

            // ContentRootFileProvider is the default one and is always available.
            var fileProvider = new CompositeFileProvider(new[] { hostingEnvironment.ContentRootFileProvider }.Concat(resouceFileProviders));

            mvc
                .Services
                .AddSingleton<IFileProvider>(fileProvider);

            mvc
                .Services
                .Configure<RazorViewEngineOptions>(options =>
                {
                    options
                        .FileProviders
                        .Add(fileProvider);
                });


            return mvc;
        }

        private static IEnumerable<string> GetExtensionDirectories(IServiceProvider serviceProvider)
        {
            var configuration = serviceProvider.GetService<IConfiguration>();
            var hostingEnvironment = serviceProvider.GetService<IHostingEnvironment>();

            var extRootPath = Path.Combine(hostingEnvironment.ContentRootPath, configuration["Extensibility:Ext"]);

            return
                Directory.Exists(extRootPath)
                    ? Directory.GetDirectories(extRootPath)
                    : Enumerable.Empty<string>();
        }

        private static Assembly LoadExtensionAssembly(IServiceProvider serviceProvider, string extensionDirectory)
        {
            var hostingEnvironment = serviceProvider.GetService<IHostingEnvironment>();
            var configuration = serviceProvider.GetService<IConfiguration>();

            var binDirectoryName = 
                hostingEnvironment.IsDevelopmentExt()
                    ? @"bin\Debug\net47\win81-x64"
                    : configuration["Extensibility:Bin"];

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

            return
                File.Exists(extensionDllName)
                    ? Assembly.LoadFile(extensionDllName)
                    : throw new FileNotFoundException($"{extensionDllName} not found.");
        }

        private static IEnumerable<Assembly> GetExtensionAssemblies(IServiceProvider serviceProvider)
        {
            var configuration = serviceProvider.GetService<IConfiguration>();
            var hostingEnvironment = serviceProvider.GetService<IHostingEnvironment>();
            var logger = serviceProvider.GetService<ILoggerFactory>().CreateLogger<Startup>();

            if (hostingEnvironment.IsDevelopmentExt())
            {
                yield break;
            }

            var extRootPath = Path.Combine(hostingEnvironment.ContentRootPath, configuration["Extensibility:Ext"]);
            var binDirectoryName = configuration["Extensibility:Bin"];

            if (!Directory.Exists(extRootPath))
            {
                yield break;
            }

            var pluginDirectories = Directory.GetDirectories(extRootPath);
            foreach (var pluginDirectory in pluginDirectories)
            {
                // Extension assemblies are located in the {Extensibility:Ext}: ..\ext\Foo\bin\Foo.dll
                var extFullName =
                    Path.Combine(
                        pluginDirectory,
                        binDirectoryName,
                        $"{Path.GetFileName(pluginDirectory)}.dll"
                    );

                if (File.Exists(extFullName))
                {
                    var extAssembly = default(Assembly);
                    try
                    {
                        extAssembly = Assembly.LoadFile(extFullName);
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        logger.Log(Abstraction.Layer.Infrastructure().Routine(nameof(Assembly.LoadFile)).Faulted(), ex);
                    }

                    if (!(extAssembly is null))
                    {
                        yield return extAssembly;
                    }
                }
            }
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

                // C:\..\ext\Plugin\bin\PluginDependency.dll
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

        private static IEnumerable<string> EnumerateResourceDirectories(IServiceProvider serviceProvider)
        {
            var hostingEnvironment = serviceProvider.GetService<IHostingEnvironment>();
            var configuration = serviceProvider.GetService<IConfiguration>();

            // Razor view engine requires this path too.
            yield return Path.Combine(hostingEnvironment.ContentRootPath, configuration["Extensibility:Ext"]);

            // These paths are available when running as a service with installed plugins.
            foreach (var extensionDirectory in GetExtensionDirectories(serviceProvider))
            {
                yield return extensionDirectory;
            }

            if (hostingEnvironment.IsDevelopmentExt())
            {
                // These paths are available when running in developmentExt with extension outside in the app folder.
                foreach (var directory in GetDevelopmentExtensionDirectories(serviceProvider))
                {
                    yield return directory;
                }
            }
        }

        private static IEnumerable<string> GetDevelopmentExtensionDirectories(IServiceProvider serviceProvider)
        {
            var configuration = serviceProvider.GetService<IConfiguration>();
            var hostingEnvironment = serviceProvider.GetService<IHostingEnvironment>();

            // Extension development does not use plugins so we have to look for them in the current directory parent 
            // because the service is "installed" as a submodule which is a subdirectory.

            // ContentRootPath is the path of the *.csproj, we have to go back two levels to reach the extension directory.
            //var extensionsRootDirectory =
            //    new DirectoryInfo(hostingEnvironment.ContentRootPath).Parent?.Parent
            //    ?? throw new DirectoryNotFoundException("Could not find extension directory.");

            var solutionExtensions = configuration["Extensibility:Development:SolutionDirectory"];

            yield return solutionExtensions;

            var projectNames = configuration.GetSection("Extensibility:Development:ProjectNames").GetChildren().AsEnumerable().Select(x => x.Value);

            // This path is required to find static files by css-provider.
            //yield return extensionsRootDirectory.FullName;

            ////return extensionsRootDirectory.FullName;
            //foreach (var directory in Directory.EnumerateDirectories(extensionsRootDirectory.FullName, "Mailr.Extensions.*"))
            foreach (var directory in projectNames.Select(projectName => Path.Combine(solutionExtensions, projectName)))
            {
                yield return directory;
            }
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