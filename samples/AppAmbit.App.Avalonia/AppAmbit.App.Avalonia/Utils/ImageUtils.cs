using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AppAmbitTestingAppAvalonia.Models;
using Avalonia.Media.Imaging;

namespace AppAmbitTestingAppAvalonia.Utils;

internal static class ImageUtils
{
    private static readonly HttpClient _http = new();

    internal static readonly ConcurrentDictionary<string, Bitmap?> BitmapCache = new();

    internal static async Task LoadAsync(IEnumerable<CmsExampleModel> items)
    {
        var tasks = items
            .Where(i => !string.IsNullOrWhiteSpace(i.FeaturedImageUrl))
            .Select(async item =>
            {
                var url = item.FeaturedImageUrl!;
                if (BitmapCache.ContainsKey(url)) return;
                try
                {
                    var bytes = await _http.GetByteArrayAsync(url);
                    using var ms = new MemoryStream(bytes);
                    BitmapCache[url] = new Bitmap(ms);
                }
                catch
                {
                    BitmapCache[url] = null;
                }
            });

        await Task.WhenAll(tasks);
    }
}
