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
    [PublicAPI]
    [UsedImplicitly]
    [HtmlTargetElement("style")]
    public class ImportCssTagHelper : TagHelper
    {
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IConfiguration _configuration;
        private readonly IFileProvider _fileProvider;

        public ImportCssTagHelper
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
            var styles = new List<string>();

            foreach (var cssFile in GetCss().Where(cssFile => cssFile.Exists))
            {
                using (var readStream = cssFile.CreateReadStream())
                using (var reader = new StreamReader(readStream))
                {
                    styles.Add(await reader.ReadToEndAsync());
                }
            }

            output.Content.SetHtmlContent(Environment.NewLine + styles.Join(Environment.NewLine));

            // todo - add error styles here
        }

        [NotNull]
        private IEnumerable<IFileInfo> GetCss()
        {
            var urlHelper = _urlHelperFactory.GetUrlHelper(ViewContext);
            
            var theme = ViewContext.HttpContext.EmailMetadata()?.Theme ?? "default";
                        
            var mainCssFileName = urlHelper.RouteUrl(RouteNames.Themes, new { theme});
            yield return _fileProvider.GetFileInfo(mainCssFileName);

            // First, try to get the css by theme.
            var extension = ViewContext.HttpContext.ExtensionId();
            var controllerType = ViewContext.HttpContext.ControllerType();

            var cssRouteName = RouteNameFactory.CreateCssRouteName(controllerType, true);
            var cssFileName = urlHelper.RouteUrl(cssRouteName, new { extension, theme });

            var cssFile = _fileProvider.GetFileInfo(cssFileName);
            if (cssFile.Exists)
            {
                yield return cssFile;
            }

            else
            {
                // Otherwise fallback to the default css.
                cssRouteName = RouteNameFactory.CreateCssRouteName(controllerType, false);
                cssFileName = urlHelper.RouteUrl(cssRouteName, new { extension });
                yield return _fileProvider.GetFileInfo(cssFileName);
            }
        }
    }
}