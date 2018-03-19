using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.FileProviders;
using Reusable;

namespace Mailr.Helpers
{
    public interface ICssProvider
    {
        Task<Css> GetCss(string fileName);
    }

    public class CssProvider : ICssProvider
    {
        private readonly IFileProvider _fileProvider;

        private readonly ConcurrentDictionary<SoftString, Task<Css>> _cache = new ConcurrentDictionary<SoftString, Task<Css>>();

        public CssProvider(IFileProvider fileProvider)
        {
            _fileProvider = fileProvider;
        }

        public Task<Css> GetCss(string fileName)
        {
            //Debug.WriteLine($"{nameof(GetCss)}: {fileName}");
            return _cache.GetOrAdd(fileName, async (cssFilename) =>
            {
                var cssFile = _fileProvider.GetFileInfo(fileName);
                if (cssFile.Exists)
                {
                    using (var reader = new StreamReader(cssFile.CreateReadStream()))
                    {
                        var cssString = await reader.ReadToEndAsync();
                        return CssParser.Default.Parse(cssString);
                    }
                }
                else
                {
                    return new Css();
                }
            });
        }
    }
}