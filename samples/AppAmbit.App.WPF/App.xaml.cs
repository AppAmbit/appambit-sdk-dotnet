using AppAmbit;
using System.Configuration;
using System.Data;
using System.Windows;

namespace AppAmbit.App.WPF;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppAmbitSdk.Start("<YOUR_APPKEY>");
    }
}