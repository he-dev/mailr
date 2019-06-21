using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Custom;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Mailr.Extensions.Helpers;
using Mailr.Extensions.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace Mailr.Extensions.Mvc.TagHelpers
{
    [PublicAPI]
    [UsedImplicitly]
    [HtmlTargetElement("style")]
    public class ImportCssTagHelper : TagHelper
    {
        private readonly IUrlHelperFactory _urlHelperFactory;

        //private readonly IConfiguration _configuration;
        private readonly IFileProvider _fileProvider;

        public ImportCssTagHelper
        (
            IUrlHelperFactory urlHelperFactoryHelperFactory,
            //IConfiguration configuration,
            IFileProvider fileProvider
        )
        {
            _urlHelperFactory = urlHelperFactoryHelperFactory;
            //_configuration = configuration;
            _fileProvider = fileProvider;
        }

        [HtmlAttributeNotBound, ViewContext]
        public ViewContext ViewContext { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var styles = new List<string>();

            var css =
                from cssFileName in CssFileNames()
                let fileInfo = _fileProvider.GetFileInfo(cssFileName)
                where fileInfo.Exists
                select fileInfo;

            foreach (var cssFile in css)
            {
                using (var readStream = cssFile.CreateReadStream())
                using (var reader = new StreamReader(readStream))
                {
                    styles.Add(await reader.ReadToEndAsync());
                }
            }

            output.Content.SetHtmlContent(Environment.NewLine + styles.Join(Environment.NewLine));
        }

        private IEnumerable<string> CssFileNames()
        {
            var urlHelper = _urlHelperFactory.GetUrlHelper(ViewContext);

            yield return urlHelper.RouteUrl(RouteNames.Css.Global, new { theme = "default" });
            yield return urlHelper.RouteUrl(RouteNames.Css.Extension, new { theme = "default" });

            if (ViewContext.HttpContext.Items[HttpContextItemNames.EmailTheme] is string theme)
            {
                yield return urlHelper.RouteUrl(RouteNames.Css.Global, new { theme });
                yield return urlHelper.RouteUrl(RouteNames.Css.Extension, new { theme });
            }
        }
    }
}