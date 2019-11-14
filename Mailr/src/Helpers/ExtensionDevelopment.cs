using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Mailr.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Mailr.Helpers
{
    public static class ExtensionDevelopment
    {
        public static IEnumerable<string> EnumerateExtensionDirectories(this IServiceCollection services)
        {
            var serviceProvider = services.BuildServiceProvider();
            var hostingEnvironment = serviceProvider.GetService<IHostingEnvironment>();
            
            if (hostingEnvironment.IsDevelopmentExt())
            {
                var xMailr = XDocument.Load(Path.Combine(hostingEnvironment.ContentRootPath, "Mailr-dev.csproj"));

                return 
                    xMailr
                        .Root
                        .Elements("ItemGroup")
                        .SelectMany(x => x.Elements("ProjectReference"))
                        .Select(x => x.Attribute("Include").Value)
                        .Where(x => Regex.IsMatch(x, @"Mailr\.Extensions\.\w+\.csproj"))
                        .Select(Path.GetDirectoryName);
            }
            else
            {
                return Enumerable.Empty<string>();
            }
        }
    }
}