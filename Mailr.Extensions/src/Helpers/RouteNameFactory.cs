namespace Mailr.Extensions.Helpers
{
    internal static class RouteNameFactory
    {
        public static string CreateCssRouteName(ControllerType controllerType, bool useCustomTheme)
        {
            return $"{controllerType}-extension{(useCustomTheme ? "-with-theme" : default)}";
        }
    }
}