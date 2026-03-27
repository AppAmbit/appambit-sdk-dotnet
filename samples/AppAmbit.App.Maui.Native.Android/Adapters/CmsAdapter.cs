using System.Collections.Generic;
using Android.Content;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using AppAmbitTestingAppAndroid.Models;

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
        {
            var item = _items[position];
            vh.Bind(item);
        }
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
        private readonly TextView _tvProductName;
        private readonly TextView _tvCategory;
        private readonly TextView _tvDescription;
        private readonly TextView _tvSku;
        private readonly TextView _tvPrice;
        private readonly TextView _tvStock;
        private readonly TextView _tvSupport;
        private readonly TextView _tvId;
        private readonly TextView _tvCreatedAt;
        private readonly TextView _tvPublishedAt;
        private readonly TextView _tvUpdatedAt;

        public CmsViewHolder(View itemView) : base(itemView)
        {
            var ctx = itemView.Context!;
            int GetId(string name) => ctx.Resources!.GetIdentifier(name, "id", ctx.PackageName);

            _tvProductName = itemView.FindViewById<TextView>(GetId("tv_product_name"))!;
            _tvCategory = itemView.FindViewById<TextView>(GetId("tv_category"))!;
            _tvDescription = itemView.FindViewById<TextView>(GetId("tv_description"))!;
            _tvSku = itemView.FindViewById<TextView>(GetId("tv_sku"))!;
            _tvPrice = itemView.FindViewById<TextView>(GetId("tv_price"))!;
            _tvStock = itemView.FindViewById<TextView>(GetId("tv_stock"))!;
            _tvSupport = itemView.FindViewById<TextView>(GetId("tv_support"))!;
            _tvId = itemView.FindViewById<TextView>(GetId("tv_id"))!;
            _tvCreatedAt = itemView.FindViewById<TextView>(GetId("tv_created_at"))!;
            _tvPublishedAt = itemView.FindViewById<TextView>(GetId("tv_published_at"))!;
            _tvUpdatedAt = itemView.FindViewById<TextView>(GetId("tv_updated_at"))!;
        }

        public void Bind(CmsExampleModel item)
        {
            _tvProductName.Text = item.ProductName;
            _tvCategory.Text = string.IsNullOrEmpty(item.Category) ? "" : $"🏷️ {item.Category}";
            _tvDescription.Text = item.Description;
            _tvSku.Text = item.ItemSku;
            _tvPrice.Text = $"${item.Price:F2}";
            _tvStock.Text = $"Stock: {item.InStock}";
            _tvSupport.Text = $"📧 {item.SupportEmail}";
            _tvId.Text = $"ID: {item.Id}";
            _tvCreatedAt.Text = $"Cr: {item.CreatedAt:dd/MM/yyyy}";
            _tvPublishedAt.Text = $"Pub: {item.PublishedAt:dd/MM/yyyy}";
            _tvUpdatedAt.Text = $"Upd: {item.UpdatedAt:dd/MM/yyyy}";
        }
    }
}
