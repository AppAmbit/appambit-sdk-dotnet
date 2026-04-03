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

    /// <summary>
    /// Downloads and decodes images for all items in parallel, populating
    /// <see cref="CmsExampleModel.ProductBitmap"/>. Items without a URL are skipped.
    /// </summary>
    internal static async Task LoadAsync(IEnumerable<CmsExampleModel> items)
    {
        var tasks = items
            .Where(i => !string.IsNullOrWhiteSpace(i.ProductImageUrl))
            .Select(async item =>
            {
                try
                {
                    var bytes = await _http.GetByteArrayAsync(item.ProductImageUrl!);
                    using var ms = new MemoryStream(bytes);
                    item.ProductBitmap = new Bitmap(ms);
                }
                catch { }
            });

        await Task.WhenAll(tasks);
    }
}
