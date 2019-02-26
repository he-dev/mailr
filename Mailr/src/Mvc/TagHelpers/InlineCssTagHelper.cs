using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Custom;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Mailr.Extensions.Utilities;
using Mailr.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Reusable;
using Reusable.Extensions;

namespace Mailr.Mvc.TagHelpers
{
    [UsedImplicitly]
    [HtmlTargetElement("style")]
    public class InlineCssTagHelper : TagHelper
    {
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IConfiguration _configuration;
        private readonly IFileProvider _fileProvider;

        public InlineCssTagHelper
        (
            IUrlHelperFactory urlHelperFactoryHelperFactory,
            IConfiguration configuration,
            IFileProvider fileProvider
        )
        {
            _urlHelperFactory = urlHelperFactoryHelperFactory;
            _configuration = configuration;
            _fileProvider = fileProvider;
        }

        [HtmlAttributeNotBound, ViewContext]
        public ViewContext ViewContext { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var url = _urlHelperFactory.GetUrlHelper(ViewContext);
            var theme = ViewContext.HttpContext.EmailMetadata()?.Theme ?? "default";
            var themeCssFileName = url.RouteUrl(RouteNames.Themes, new { name = theme });

            var cssRouteName = ViewContext.HttpContext.ControllerType().ToString();
            var extensionCssFileName = url.RouteUrl(cssRouteName, new { extension = ViewContext.HttpContext.ExtensionId() });
            var themeCss = _fileProvider.GetFileInfo(extensionCssFileName);

            if (themeCss.Exists)
            {
                using (var readStream = themeCss.CreateReadStream())
                using (var reader = new StreamReader(readStream))
                {
                    var css = await reader.ReadToEndAsync();
                    output.Content.SetHtmlContent(Environment.NewLine + css);
                }
            }
            else
            {
                // todo - add error styles here
            }
        }
    }
}