using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Mailr.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.DependencyModel.Resolution;
using Microsoft.Extensions.FileProviders;
using Reusable.OmniLog;
using Reusable.OmniLog.Abstractions;
using Reusable.OmniLog.Nodes;
using Reusable.OmniLog.Extensions;

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

//            mvc
//                .ConfigureApplicationPartManager(apm =>
//                {
//                    //apm.FeatureProviders.Insert(0, new test());
//                    var binDirectory = extensibility.Bin;
//
//                    // Skip the first directory which is the root and does not contain any extensions.
//                    foreach (var extensionDirectory in extensionDirectoriesWithoutRoot)
//                    {
//                        // Mailr.Extensions.Example
//                        var extensionName = Path.GetFileName(extensionDirectory) + ".dll";
//
//                        // Extension assemblies are located in the {Extensibility:Ext}: ..\ext\Foo\bin\Foo.dll
//                        var extensionFullName = Path.Combine
//                        (
//                            extensionDirectory,
//                            binDirectory,
//                            extensionName
//                        );
//
//                        //if (TryLoadAssembly(serviceProvider, extensionFullName, out var extensionAssembly))
//                        //{
//                        //    //var parts = new CompiledRazorAssemblyApplicationPartFactory().GetApplicationParts(extensionAssembly);
//                        //    //foreach (var applicationPart in parts)
//                        //    //{
//                        //    //    apm.ApplicationParts.Add(applicationPart);
//                        //    //}
//
//                        //    apm.ApplicationParts.Add(new AssemblyPart(extensionAssembly));
//                        //    //apm.ApplicationParts.Add(new CompiledRazorAssemblyPart(extensionAssembly));
//
//                        //    //mvc.Services.Configure<RazorViewEngineOptions>(o => o.FileProviders.Add(new EmbeddedFileProvider(extensionAssembly)));
//                        //}
//                    }
//                });

            //ConfigureAssemblyResolve(serviceProvider, extensionDirectoriesWithoutRoot);

            var staticFileProviders =
                extensionDirectories
                    .Select(path => new PhysicalFileProvider(path))
                    .Prepend(hostingEnvironment.ContentRootFileProvider)
                    .ToList();

            

            // ContentRootFileProvider is the default one and is always available.
            var fileProvider = new CompositeFileProvider(staticFileProviders);

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

        public static IEnumerable<string> EnumerateExtensionDirectories(this IApplicationBuilder app)
        {
            var serviceProvider = app.ApplicationServices;
            var hostingEnvironment = serviceProvider.GetService<IHostingEnvironment>();

            return
                hostingEnvironment.IsDevelopmentExt()
                    ? EnumerateExtensionProjectDirectories(serviceProvider)
                    : EnumerateExtensionInstallationDirectories(serviceProvider);
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

            //AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
            AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
            {
                // Extract dependency name from the full assembly name:
                // FooPlugin.FooClass, Version = 1.0.0.0, Culture = neutral, PublicKeyToken = null
                var dependencyName = assemblyName.Name.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).First() + ".dll";

                using (logger.BeginScope("ResolveAssembly"))
                {
                    //logger.Log(Abstraction.Layer.Service().Meta(new { DependencyName = dependencyName, RequestingAssembly = e.RequestingAssembly?.GetName().Name }));

                    // Try the current directory first...
                    var dependencyFullName = Path.Combine(exeDirectory, dependencyName);
                    if (TryLoadAssembly(serviceProvider, dependencyFullName, out var assembly))
                    {
                        return assembly;
                    }

                    // ...the try extension directories.
                    // C:\..\ext\Foo\bin\Bar.dll
                    foreach (var directory in extensionDirectories)
                    {
                        dependencyFullName = Path.Combine(directory, binDirectoryName, dependencyName);
                        if (TryLoadAssembly(serviceProvider, dependencyFullName, out assembly))
                        {
                            return assembly;
                        }
                    }

                    return null;
                }
            };
        }

        private static bool TryLoadAssembly(IServiceProvider serviceProvider, string fileName, out Assembly assembly)
        {
            var logger = serviceProvider.GetService<ILoggerFactory>().CreateLogger<Startup>();
            logger.Log(Abstraction.Layer.Service().Meta(new { fileName }));

            try
            {
                if (File.Exists(fileName))
                {
                    //assembly = Assembly.LoadFile(fileName);
                    assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(fileName);

                    var dependencyContext = DependencyContext.Load(assembly);

                    var loadContext = AssemblyLoadContext.GetLoadContext(assembly);

                    var assemblyResolver = new CompositeCompilationAssemblyResolver
                    (new ICompilationAssemblyResolver[]
                    {
                        new AppBaseCompilationAssemblyResolver(Path.GetDirectoryName(fileName)),
                        new ReferenceAssemblyPathResolver(),
                        new PackageCompilationAssemblyResolver()
                    });

                    loadContext.Resolving += (context, assemblyName) =>
                    {
                        bool NamesMatch(RuntimeLibrary runtime)
                        {
                            return string.Equals(runtime.Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase);
                        }

                        var library = dependencyContext?.RuntimeLibraries.FirstOrDefault(NamesMatch);
                        if (library != null)
                        {
                            var wrapper = new CompilationLibrary(
                                library.Type,
                                library.Name,
                                library.Version,
                                library.Hash,
                                library.RuntimeAssemblyGroups.SelectMany(g => g.AssetPaths),
                                library.Dependencies,
                                library.Serviceable);

                            var assemblies = new List<string>();
                            assemblyResolver.TryResolveAssemblyPaths(wrapper, assemblies);
                            if (assemblies.Count > 0)
                            {
                                return loadContext.LoadFromAssemblyPath(assemblies[0]);
                            }
                        }

                        return default;
                    };

                    //logger.Log(Abstraction.Layer.Service().Routine(nameof(TryLoadAssembly)).Completed());
                    return true;
                }
                else
                {
                    //logger.Log(Abstraction.Layer.Service().Routine(nameof(TryLoadAssembly)).Canceled(), "File not found.");
                }
            }
            catch (Exception ex)
            {
                //logger.Log(Abstraction.Layer.Service().Routine(nameof(TryLoadAssembly)).Faulted(), ex);

                if (ex is ReflectionTypeLoadException inner)
                {
                    foreach (var loaderException in inner.LoaderExceptions)
                    {
                        //logger.Log(Abstraction.Layer.Service().Routine(nameof(TryLoadAssembly)).Faulted(), nameof(ReflectionTypeLoadException), loaderException);
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

    internal class test : IApplicationFeatureProvider<Microsoft.AspNetCore.Mvc.ViewComponents.ViewComponentFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ViewComponentFeature feature) { }
    }
}