using System.Collections.Generic;
using Android.Content;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using AppAmbitTestingAppAndroid.Models;
using AppAmbitTestingAppAndroid.Utils;

namespace AppAmbitTestingAppAndroid.Adapters;

public class CmsAdapter : RecyclerView.Adapter
{
    private readonly List<CmsExampleModel> _items = new();

    public override int ItemCount => _items.Count;

    public void UpdateData(IEnumerable<CmsExampleModel> newItems)
    {
        _items.Clear();
        _items.AddRange(newItems);
        NotifyDataSetChanged();
    }

    public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
    {
        if (holder is CmsViewHolder vh)
            vh.Bind(_items[position]);
    }

    public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
    {
        var ctx = parent.Context!;
        int layoutId = ctx.Resources!.GetIdentifier("item_cms", "layout", ctx.PackageName);
        var view = LayoutInflater.From(ctx)!.Inflate(layoutId, parent, false);
        return new CmsViewHolder(view!);
    }

    private class CmsViewHolder : RecyclerView.ViewHolder
    {
        private readonly ImageView _ivFeaturedImage;
        private readonly TextView _tvTitle;
        private readonly TextView _tvAuthor;
        private readonly TextView _tvBody;
        private readonly TextView _tvLikes;
        private readonly TextView _tvRating;
        private readonly TextView _tvReadingTime;
        private readonly TextView _tvCategory;
        private readonly TextView _tvIsPublished;
        private readonly TextView _tvPublishedAt;

        public CmsViewHolder(View itemView) : base(itemView)
        {
            var ctx = itemView.Context!;
            int GetId(string name) => ctx.Resources!.GetIdentifier(name, "id", ctx.PackageName);

            _ivFeaturedImage = itemView.FindViewById<ImageView>(GetId("iv_featured_image"))!;
            _tvTitle         = itemView.FindViewById<TextView>(GetId("tv_title"))!;
            _tvAuthor        = itemView.FindViewById<TextView>(GetId("tv_author"))!;
            _tvBody          = itemView.FindViewById<TextView>(GetId("tv_body"))!;
            _tvLikes         = itemView.FindViewById<TextView>(GetId("tv_likes"))!;
            _tvRating        = itemView.FindViewById<TextView>(GetId("tv_rating"))!;
            _tvReadingTime   = itemView.FindViewById<TextView>(GetId("tv_reading_time"))!;
            _tvCategory      = itemView.FindViewById<TextView>(GetId("tv_category"))!;
            _tvIsPublished   = itemView.FindViewById<TextView>(GetId("tv_is_published"))!;
            _tvPublishedAt   = itemView.FindViewById<TextView>(GetId("tv_published_at"))!;
        }

        public void Bind(CmsExampleModel item)
        {
            _tvTitle.Text       = item.Title;
            _tvAuthor.Text      = !string.IsNullOrWhiteSpace(item.AuthorEmail) ? $"✍️ {item.AuthorEmail}" : "";
            _tvBody.Text        = item.Body;
            _tvLikes.Text       = $"👁️ {item.ViewsCount:N0}";
            _tvRating.Text      = !string.IsNullOrWhiteSpace(item.EventDate) ? $"📅 {item.EventDate}" : "";
            _tvReadingTime.Text = "";
            _tvCategory.Text    = item.Category?.Count > 0 ? string.Join(", ", item.Category) : "";
            _tvIsPublished.Text = item.IsPublished == true ? "✓ Published" : "✕ Draft";
            _tvPublishedAt.Text = !string.IsNullOrWhiteSpace(item.PublishedAt) ? $"🗓 {item.PublishedAt}" : "";

            _ivFeaturedImage.SetImageDrawable(null);
            if (!string.IsNullOrWhiteSpace(item.FeaturedImageUrl))
                ImageUtils.LoadAsync(item.FeaturedImageUrl!, _ivFeaturedImage, item.Id ?? item.FeaturedImageUrl!);
        }
    }
}
