using System.Diagnostics;
using AppAmbit;

namespace AppAmbitTestingApp;

public partial class RemoteConfigPage : ContentPage
{
    public RemoteConfigPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateUI();
    }

    private void UpdateUI()
    {
        bool showBanner = RemoteConfig.GetBoolean("banner");
        string dataText = RemoteConfig.GetString("data");
        int discount = RemoteConfig.GetInt("discount");

        BannerView.IsVisible = showBanner;
        DataLabel.Text = dataText;
        DiscountLabel.Text = $"{discount}% OFF";
    }
}