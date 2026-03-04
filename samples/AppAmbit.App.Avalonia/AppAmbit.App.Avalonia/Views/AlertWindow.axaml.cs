using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace AppAmbitTestingAppAvalonia.Views;

public partial class AlertWindow : Window
{
    public AlertWindow()
    {
        InitializeComponent();
        btnOk.Click += BtnOk_Click;
    }

    private void BtnOk_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    public void SetMessage(string message)
    {
        txtMessage.Text = message;
    }

    public static async Task ShowAlert(string message)
    {
        try
        {
            var app = Avalonia.Application.Current;
            if (app?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var win = new AlertWindow();
                win.SetMessage(message);
                if (desktop.MainWindow != null)
                {
                    await win.ShowDialog(desktop.MainWindow);
                    return;
                }
                else
                {
                    win.Show();
                    return;
                }
            }

            if (app?.ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            {
                var control = singleView.MainView as Control;
                if (control != null)
                {
                    var grid = control.FindControl<Grid>("ContentGrid");
                    Border overlay = null;
                    if (grid != null)
                    {
                        overlay = CreateOverlay(message);
                        grid.Children.Add(overlay);
                    }
                    else if (control is Panel panel)
                    {
                        overlay = CreateOverlay(message);
                        panel.Children.Add(overlay);
                    }

                    if (overlay != null)
                    {
                        await Task.Delay(2200);
                        if (grid != null)
                            grid.Children.Remove(overlay);
                        else if (control is Panel panel2)
                            panel2.Children.Remove(overlay);
                    }
                }
            }
        }
        catch {}
    }

    private static Border CreateOverlay(string message)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            Child = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = message,
                    Foreground = Brushes.Black,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    MaxWidth = 320
                }
            }
        };
    }
}
