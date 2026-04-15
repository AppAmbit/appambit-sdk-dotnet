using System.Net.Http;
using System.Threading.Tasks;
using Foundation;
using UIKit;

namespace AppAmbitTestingiOS.Utils;

internal static class ImageUtils
{
    private static readonly HttpClient _http = new();

    /// <summary>
    /// Loads an image from <paramref name="url"/> on a background thread and sets it on
    /// <paramref name="imageView"/> on the UI thread. Uses <paramref name="tag"/> to discard
    /// results that arrive after the cell has been recycled to a different item.
    /// </summary>
    internal static void LoadAsync(string url, UIImageView imageView, object tag)
    {
        imageView.Image = null;
        imageView.AccessibilityIdentifier = tag.ToString();

        _ = Task.Run(async () =>
        {
            try
            {
                var bytes = await _http.GetByteArrayAsync(url);
                var data = NSData.FromArray(bytes);
                var img = UIImage.LoadFromData(data);
                if (img != null && imageView.AccessibilityIdentifier == tag.ToString())
                    imageView.InvokeOnMainThread(() => imageView.Image = img);
            }
            catch { }
        });
    }
}
