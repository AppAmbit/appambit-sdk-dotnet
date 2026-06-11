using System.Collections.Generic;
using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;

namespace AppAmbitTestingAppAndroid.Adapters;

public class DbRowAdapter : RecyclerView.Adapter
{
    private List<string> _columns = new();
    private List<Dictionary<string, object?>> _rows = new();

    public override int ItemCount => _rows.Count;

    public void UpdateData(List<string> columns, List<Dictionary<string, object?>> rows)
    {
        _columns = columns;
        _rows = rows;
        NotifyDataSetChanged();
    }

    public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
    {
        if (holder is DbRowViewHolder vh)
            vh.Bind(_columns, _rows[position]);
    }

    public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
    {
        var ctx = parent.Context!;
        int layoutId = ctx.Resources!.GetIdentifier("item_db_row", "layout", ctx.PackageName);
        var view = LayoutInflater.From(ctx)!.Inflate(layoutId, parent, false);
        return new DbRowViewHolder(view!);
    }

    private class DbRowViewHolder : RecyclerView.ViewHolder
    {
        private readonly LinearLayout _container;
        private readonly Context _ctx;

        public DbRowViewHolder(View itemView) : base(itemView)
        {
            _ctx = itemView.Context!;
            int containerId = _ctx.Resources!.GetIdentifier("row_container", "id", _ctx.PackageName);
            _container = itemView.FindViewById<LinearLayout>(containerId)!;
        }

        public void Bind(List<string> columns, Dictionary<string, object?> row)
        {
            _container.RemoveAllViews();

            foreach (var col in columns)
            {
                var val = row.TryGetValue(col, out var v) ? v?.ToString() ?? "null" : "null";

                var cell = new LinearLayout(_ctx)
                {
                    Orientation = Orientation.Vertical
                };
                cell.SetPadding(8, 4, 16, 4);

                var header = new TextView(_ctx);
                header.Text = col;
                header.SetTextSize(Android.Util.ComplexUnitType.Sp, 10);
                header.SetTextColor(Color.Gray);
                header.SetTypeface(null, TypefaceStyle.Bold);

                var value = new TextView(_ctx);
                value.Text = val;
                value.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
                value.SetTextColor(Color.Black);
                value.SetTypeface(Typeface.Monospace, TypefaceStyle.Normal);

                cell.AddView(header);
                cell.AddView(value);
                _container.AddView(cell);
            }
        }
    }
}
