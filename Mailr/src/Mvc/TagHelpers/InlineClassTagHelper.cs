using System;
using System.Collections.Generic;
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
using Reusable;
using Reusable.Extensions;

namespace Mailr.Mvc.TagHelpers
{
    [UsedImplicitly]
    [HtmlTargetElement(Attributes = "class")]
    public class InlineClassTagHelper : TagHelper
    {
        private readonly ICssProvider _cssProvider;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IConfiguration _configuration;

        public InlineClassTagHelper(ICssProvider cssProvider, IUrlHelperFactory urlHelperFactoryHelperFactory, IConfiguration configuration)
        {
            _cssProvider = cssProvider;
            _urlHelperFactory = urlHelperFactoryHelperFactory;
            _configuration = configuration;
        }

        [HtmlAttributeNotBound, ViewContext]
        public ViewContext ViewContext { get; set; }

        private string ClassPrefix => _configuration["CssInliner:ClassPrefix"] ?? throw new InvalidOperationException("You need to define 'CssInliner:ClassPrefix' in the 'appSettings.json' file.");

        private string ClassNotFoundStyle => _configuration["CssInliner:ClassNotFoundStyle"] ?? throw new InvalidOperationException("You need to define 'CssInliner:ClassNotFoundStyle' in the 'appSettings.json' file.");

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var classNames =
                output
                    .Attributes["class"]
                    ?.Value
                    .ToString()
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (classNames is null)
            {
                return;
            }

            var classPrefix = ClassPrefix;

            var inlineableClassNames =
                (from className in classNames
                 where className?.StartsWith(classPrefix) ?? false
                 select SoftString.Create(className)).ToList();

            if (inlineableClassNames.Empty())
            {
                return;
            }

            inlineableClassNames =
                inlineableClassNames
                    .Select(className => $".{className.ToString()}".ToSoftString())
                    .ToList();

            var url = _urlHelperFactory.GetUrlHelper(ViewContext);

            var theme = ViewContext.HttpContext.EmailMetadata()?.Theme ?? "default";

            var themeCssFileName = url.RouteUrl(RouteNames.Themes, new { name = theme });

            var route = ViewContext.HttpContext.ExtensionType().ToString();
            var extensionCssFileName = url.RouteUrl(route, new { extension = ViewContext.HttpContext.ExtensionId() });

            var themeCss = await _cssProvider.GetCss(themeCssFileName);
            var extensionCss = extensionCssFileName is null ? new List<CssRuleset>() : await _cssProvider.GetCss(extensionCssFileName);

            var declarations =
                from ruleset in themeCss.Concat(extensionCss)
                from selector in ruleset.Selectors
                join className in inlineableClassNames on selector equals className
                select ruleset.Declarations.TrimEnd(';');

            var style = declarations.Join("; ");

            if (style.IsNullOrEmpty())
            {
                // Make debugging of missing styles easier by highlighting the element with a red border.
                output.Attributes.SetAttribute("style", ClassNotFoundStyle);
            }
            else
            {
                output.Attributes.SetAttribute("style", style);
                output.Attributes.RemoveAll("class");
            }
        }
    }
}