using System;
using CoreGraphics;
using UIKit;
using AppAmbitMaui;
using AppAmbit;

namespace AppAmbitTestingMacOs;

public class RemoteConfigViewController : UIViewController
{
    UIView? _bannerView;
    UILabel? _dataLabel;
    UILabel? _discountLabel;
    
    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        if (OperatingSystem.IsIOSVersionAtLeast(13))
            View!.BackgroundColor = UIColor.SystemGroupedBackground;
        else
            View!.BackgroundColor = UIColor.LightGray;
        
        SetupUI();
    }
    
    public override void ViewWillAppear(bool animated)
    {
        base.ViewWillAppear(animated);
        UpdateValues();
    }

    void SetupUI()
    {
        // Container StackView
        var mainStack = new UIStackView
        {
            Axis = UILayoutConstraintAxis.Vertical,
            Alignment = UIStackViewAlignment.Fill,
            Spacing = 24,
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        View!.AddSubview(mainStack);

        // Banner
        _bannerView = new UIView
        {
            BackgroundColor = UIColor.SystemIndigo,
            Hidden = true,
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        _bannerView.Layer.CornerRadius = 12;
        _bannerView.Layer.ShadowColor = UIColor.Black.CGColor;
        _bannerView.Layer.ShadowOpacity = 0.2f;
        _bannerView.Layer.ShadowOffset = new CGSize(0, 4);
        _bannerView.Layer.ShadowRadius = 6;
        
        var bannerLabel = new UILabel
        {
            Text = "BANNER",
            TextColor = UIColor.White,
            Font = UIFont.SystemFontOfSize(28, UIFontWeight.Bold),
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        _bannerView.AddSubview(bannerLabel);
        
        NSLayoutConstraint.ActivateConstraints(new[]
        {
            bannerLabel.CenterXAnchor.ConstraintEqualTo(_bannerView.CenterXAnchor),
            bannerLabel.CenterYAnchor.ConstraintEqualTo(_bannerView.CenterYAnchor),
            _bannerView.HeightAnchor.ConstraintEqualTo(120)
        });
        
        mainStack.AddArrangedSubview(_bannerView);

        // Message Card
        var messageCard = CreateCard();
        var messageTitle = CreateTitleLabel("Message of the day");
        _dataLabel = new UILabel
        {
            Text = "Loading...",
            Font = UIFont.SystemFontOfSize(18),
            TextColor = UIColor.DarkGray, // Fallback safe
            Lines = 0,
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        
        if (OperatingSystem.IsIOSVersionAtLeast(13))
             _dataLabel.TextColor = UIColor.Label;

        var messageStack = new UIStackView(new UIView[] { messageTitle, _dataLabel! })
        {
            Axis = UILayoutConstraintAxis.Vertical,
            Spacing = 8,
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        messageCard.AddSubview(messageStack);
        PinToCard(messageStack, messageCard);
        mainStack.AddArrangedSubview(messageCard);

        // Discount Card
        var discountCard = CreateCard();
        var discountTitle = CreateTitleLabel("Special Discount");
        _discountLabel = new UILabel
        {
            Text = "",
            Font = UIFont.SystemFontOfSize(32, UIFontWeight.Bold),
            TextColor = UIColor.SystemIndigo,
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        var discountStack = new UIStackView(new UIView[] { discountTitle, _discountLabel! })
        {
            Axis = UILayoutConstraintAxis.Vertical,
            Spacing = 8,
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        discountCard.AddSubview(discountStack);
        PinToCard(discountStack, discountCard);
        mainStack.AddArrangedSubview(discountCard);

        // Fetch Button
        var fetchButton = new UIButton(UIButtonType.System)
        {
            BackgroundColor = UIColor.SystemIndigo,
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        fetchButton.SetTitle("Fetch test", UIControlState.Normal);
        fetchButton.SetTitleColor(UIColor.White, UIControlState.Normal);
        fetchButton.TitleLabel.Font = UIFont.BoldSystemFontOfSize(18);
        fetchButton.Layer.CornerRadius = 10;
        
        // Add padding using constraints if needed, or simply height
        fetchButton.HeightAnchor.ConstraintEqualTo(50).Active = true;

        fetchButton.TouchUpInside += async (sender, e) =>
        {
            var success = await RemoteConfig.FetchAndActivate();
            var title = success ? "Success" : "Error";
            var message = success ? "Fetch success" : "Fetch Throttled";
            
            var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
            alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
            PresentViewController(alert, true, null);
        };

        mainStack.AddArrangedSubview(fetchButton);

        // Constraints
        NSLayoutConstraint.ActivateConstraints(new[]
        {
            mainStack.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor, 24),
            mainStack.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor, 24),
            mainStack.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor, -24)
        });
    }

    UIView CreateCard()
    {
        var card = new UIView { TranslatesAutoresizingMaskIntoConstraints = false };
        card.BackgroundColor = UIColor.White;
        if (OperatingSystem.IsIOSVersionAtLeast(13))
            card.BackgroundColor = UIColor.SecondarySystemGroupedBackground;
        
        card.Layer.CornerRadius = 12;
        card.Layer.ShadowColor = UIColor.Black.CGColor;
        card.Layer.ShadowOpacity = 0.1f;
        card.Layer.ShadowOffset = new CGSize(0, 2);
        card.Layer.ShadowRadius = 4;
        return card;
    }

    UILabel CreateTitleLabel(string text)
    {
        var label = new UILabel
        {
            Text = text,
            Font = UIFont.SystemFontOfSize(14, UIFontWeight.Medium),
            TextColor = UIColor.Gray,
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        if (OperatingSystem.IsIOSVersionAtLeast(13))
            label.TextColor = UIColor.SecondaryLabel;
        return label;
    }

    void PinToCard(UIView view, UIView card)
    {
        NSLayoutConstraint.ActivateConstraints(new[]
        {
            view.TopAnchor.ConstraintEqualTo(card.TopAnchor, 16),
            view.LeadingAnchor.ConstraintEqualTo(card.LeadingAnchor, 16),
            view.TrailingAnchor.ConstraintEqualTo(card.TrailingAnchor, -16),
            view.BottomAnchor.ConstraintEqualTo(card.BottomAnchor, -16)
        });
    }

    void UpdateValues()
    {
        if (_bannerView != null) 
            _bannerView.Hidden = !RemoteConfig.GetBoolean("banner");
        
        if (_dataLabel != null)
            _dataLabel.Text = RemoteConfig.GetString("data");

        if (_discountLabel != null)
        {
            var discount = RemoteConfig.GetInt("discount");
            _discountLabel.Text = $"{discount}% OFF";
        }
    }
}
