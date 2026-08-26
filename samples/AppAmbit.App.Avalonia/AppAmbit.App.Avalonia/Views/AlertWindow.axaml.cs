using System;
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
        btnRun.Click += BtnRun_Click;
        btnCancel.Click += BtnCancel_Click;
    }

    private void BtnOk_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnRun_Click(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    public void SetMessage(string message)
    {
        txtMessage.Text = message;
    }

    private void SetConfirmationMode()
    {
        btnOk.IsVisible = false;
        btnRun.IsVisible = true;
        btnCancel.IsVisible = true;
    }

    public static async Task<bool> ShowConfirmation(string message)
    {
        try
        {
            var app = Avalonia.Application.Current;
            if (app?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow == null)
                    return false;

                var win = new AlertWindow();
                win.SetMessage(message);
                win.SetConfirmationMode();
                return await win.ShowDialog<bool>(desktop.MainWindow);
            }

            if (app?.ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            {
                var control = singleView.MainView as Control;
                if (control != null)
                    return await ShowSingleViewConfirmation(control, message);
            }
        }
        catch
        {
            return false;
        }

        return false;
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
                    Border? overlay = null;
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

    private static async Task<bool> ShowSingleViewConfirmation(Control control, string message)
    {
        var grid = control.FindControl<Grid>("ContentGrid");
        var host = grid ?? control as Panel;
        if (host == null)
            return false;

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var overlay = CreateConfirmationOverlay(message, result => completion.TrySetResult(result));
        host.Children.Add(overlay);
        try
        {
            return await completion.Task;
        }
        finally
        {
            host.Children.Remove(overlay);
        }
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

    private static Border CreateConfirmationOverlay(string message, Action<bool> complete)
    {
        var run = new Avalonia.Controls.Button { Content = "Run", Width = 80 };
        var cancel = new Avalonia.Controls.Button { Content = "Cancel", Width = 80, Margin = new Thickness(0, 0, 8, 0) };
        run.Click += (_, _) => complete(true);
        cancel.Click += (_, _) => complete(false);

        var buttons = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancel, run }
        };

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
                Child = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = message,
                            Foreground = Brushes.Black,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            MaxWidth = 320
                        },
                        buttons
                    }
                }
            }
        };
    }
}
