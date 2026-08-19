using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using AppAmbit;

namespace AppAmbit.App.WPF
{
    public partial class AnalyticsPage : Page
    {
        public AnalyticsPage()
        {
            InitializeComponent();
        }

        private void onInvalidateToken(object sender, EventArgs e)
        {
            Analytics.ClearToken();
        }

        private async void onTokenRefreshTest(object sender, EventArgs e)
        {
            Analytics.ClearToken();

            var logTasks = Enumerable.Range(0, 5)
                .Select(_ => Crashes.LogError("Sending 5 errors after an invalid token"));
            await Task.WhenAll(logTasks);

            Analytics.ClearToken();

            var eventTasks = Enumerable.Range(0, 5)
                .Select(_ => Analytics.TrackEvent(
                    "Sending 5 events after an invalid token",
                    new Dictionary<string, string> { { "Test Token", "5 events sent" } }));
            await Task.WhenAll(eventTasks);

            ShowAlert("Info", "5 events and errors sent");
        }

        private void onStartSession(object sender, EventArgs e)
        {
            Analytics.StartSession();
        }

        private void onEndSession(object sender, EventArgs e)
        {
            Analytics.EndSession();
        }

        private void onButtonClicked(object sender, EventArgs e)
        {
            Analytics.TrackEvent("Button Clicked");
        }

        private void onDefaultEvent(object sender, EventArgs e)
        {
            Analytics.GenerateTestEvent();
        }

        private void onSendMax300Event(object sender, EventArgs e)
        {
            var _300Characters = "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890";
            var _300Characters2 = "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678902";
            var properties = new Dictionary<string, string>
            {
                { _300Characters, _300Characters2 },
                { _300Characters2, _300Characters },
            };
            Analytics.TrackEvent(_300Characters, properties);
        }

        private void onSendMax20PropertiesEvent(object sender, EventArgs e)
        {
            var properties = new Dictionary<string, string>
            {
                { "01", "01" },
                { "02", "02" },
                { "03", "03" },
                { "04", "04" },
                { "05", "05" },
                { "06", "06" },
                { "07", "07" },
                { "08", "08" },
                { "09", "09" },
                { "10", "10" },
                { "11", "11" },
                { "12", "12" },
                { "13", "13" },
                { "14", "14" },
                { "15", "15" },
                { "16", "16" },
                { "17", "17" },
                { "18", "18" },
                { "19", "19" },
                { "20", "20" },
                { "21", "21" },
                { "22", "22" },
                { "23", "23" },
                { "24", "24" },
                { "25", "25" },
            };
            Analytics.TrackEvent("TestMaxProperties", properties);
        }

        private async void onGenerateBatchEvents(object sender, EventArgs e)
        {
            ShowAlert("Info", "Turn off internet");
            for(int i = 0; i < 220; i++)
            {
                Analytics.TrackEvent("Test Batch TrackEvent", new Dictionary<string, string> { { "test1", "test1" } });
            }
            ShowAlert("Info", "Events generated");
        }

        private void ShowAlert(string title, string message)
        {
            MessageBox.Show(
                message,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
    }
}
