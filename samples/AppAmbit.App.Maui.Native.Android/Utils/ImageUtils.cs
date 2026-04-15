using System.Net.Http;
using System.Threading.Tasks;
using Android.Graphics;
using Android.Widget;

namespace AppAmbitTestingAppAndroid.Utils;

internal static class ImageUtils
{
    private static readonly HttpClient _http = new();

    /// <summary>
    /// Loads an image from <paramref name="url"/> on a background thread and sets it on
    /// <paramref name="imageView"/> on the UI thread. Uses <paramref name="tag"/> to discard
    /// results that arrive after the view has been recycled to a different item.
    /// </summary>
    internal static void LoadAsync(string url, ImageView imageView, string tag)
    {
        imageView.SetImageDrawable(null);
        imageView.Tag = new Java.Lang.String(tag);

        _ = Task.Run(async () =>
        {
            try
            {
                var bytes = await _http.GetByteArrayAsync(url);
                var bmp = await BitmapFactory.DecodeByteArrayAsync(bytes, 0, bytes.Length);
                if (bmp != null && imageView.Tag?.ToString() == tag)
                    imageView.Post(() => imageView.SetImageBitmap(bmp));
            }
            catch { }
        });
    }
}
