using AppAmbit.PushNotifications;

namespace AppAmbitTest;

public class PushNotificationsTest
{
    [Fact]
    public void SetNotificationCustomizer_ShouldSetCustomizer()
    {
        // Arrange
        var customizer = new TestNotificationCustomizer();

        // Act
        PushNotifications.SetNotificationCustomizer(customizer);

        // Assert
        var retrieved = PushNotifications.GetNotificationCustomizer();
        Assert.Equal(customizer, retrieved);
    }

    [Fact]
    public void GetNotificationCustomizer_ShouldReturnSetCustomizer()
    {
        // Arrange
        PushNotifications.SetNotificationCustomizer(null);

        // Act
        var result = PushNotifications.GetNotificationCustomizer();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void SetNotificationCustomizer_WithNull_ShouldClear()
    {
        // Arrange
        PushNotifications.SetNotificationCustomizer(new TestNotificationCustomizer());

        // Act
        PushNotifications.SetNotificationCustomizer(null);

        // Assert
        Assert.Null(PushNotifications.GetNotificationCustomizer());
    }

    private class TestNotificationCustomizer : PushNotifications.INotificationCustomizer
    {
        public void Customize(object context, object builder, PushNotificationData notification)
        {
            // Mock implementation: do nothing
        }
    }
}