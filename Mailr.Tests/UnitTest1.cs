using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using Xunit;

namespace Mailr.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {

        }

        //[Fact]
        //public void ExtensionMethodAddsNewViewLocationExpander()
        //{
        //    // Arrange
        //    var services = new ServiceCollection();
        //    services.AddMvc();

        //    // These two are required to active the RazorViewEngineOptions.
        //    services.AddSingleton<IHostingEnvironment, HostingEnvironment>();
        //    services.AddSingleton<ILoggerFactory, LoggerFactory>();

        //    // Act
        //    var serviceProvider = services.BuildServiceProvider();
        //    var oldOptions = serviceProvider.GetRequiredService<IOptions<RazorViewEngineOptions>>().Value;
        //    services.AddRelativeViewLocationExpander();
        //    serviceProvider = services.BuildServiceProvider();
        //    var newOptions = serviceProvider.GetRequiredService<IOptions<RazorViewEngineOptions>>().Value;

        //    // Assert
        //    Assert.True(newOptions.ViewLocationExpanders.Count > oldOptions.ViewLocationExpanders.Count);
        //}
    }
}
