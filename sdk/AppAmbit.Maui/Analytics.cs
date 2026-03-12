namespace AppAmbitMaui;

public static class Analytics
{
    public static void EnableManualSession()
    {
        AppAmbit.Analytics.EnableManualSession();
    }

    public static async Task StartSession()
    {
        await AppAmbit.Analytics.StartSession();
    }

    public static async Task EndSession()
    {
        await AppAmbit.Analytics.EndSession();
    }

    public static async void SetUserId(string userId)
    {
        AppAmbit.Analytics.SetUserId(userId);
    }

    public static async Task<string?> GetUserId()
    {
        return await AppAmbit.Analytics.GetUserId();
    }

    public static async void SetUserEmail(string userEmail)
    {
        AppAmbit.Analytics.SetUserEmail(userEmail);
    }

    public static async Task<string?> GetUserEmail()
    {
        return await AppAmbit.Analytics.GetUserEmail();
    }

    public static async Task GenerateTestEvent()
    {
        AppAmbit.Analytics.GenerateTestEvent();
    }

    public static async Task TrackEvent(string eventName, 
            Dictionary<string, string>? properties = null)
    {
        await AppAmbit.Analytics.TrackEvent(eventName, properties);
    }

    public static void ClearToken()
    {
        AppAmbit.Analytics.ClearToken();
    }

}
