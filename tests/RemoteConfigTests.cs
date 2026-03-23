using System.Reflection;
using AppAmbit;
using AppAmbit.Enums;
using AppAmbit.Models.RemoteConfigs;
using AppAmbit.Models.Responses;
using AppAmbit.Services.Endpoints;
using AppAmbit.Services.Interfaces;
using Moq;
using Xunit;

namespace AppAmbitTest;

[Collection("RemoteConfig Tests")]
public class RemoteConfigTests : IDisposable
{
    private readonly Mock<IStorageService> _mockStorage;
    private readonly Mock<IAPIService> _mockApiService;
    private readonly Mock<IAppInfoService> _mockAppInfoService;

    public RemoteConfigTests()
    {
        _mockStorage = new Mock<IStorageService>();
        _mockApiService = new Mock<IAPIService>();
        _mockAppInfoService = new Mock<IAppInfoService>();

        ResetState();
        AppAmbit.RemoteConfig.Initialize(_mockStorage.Object, _mockAppInfoService.Object, _mockApiService.Object);

    }

    public void Dispose()
    {
        ResetState();
    }

    [Fact]
    public void GetString_ShouldReturnValueFromStorage()
    {
        // Arrange
        _mockStorage.Setup(s => s.GetConfig("banner_text"))
            .ReturnsAsync("Welcome User");

        // Act
        var value = AppAmbit.RemoteConfig.GetString("banner_text");

        // Assert
        Assert.Equal("Welcome User", value);
    }

    [Fact]
    public void GetLong_ShouldReturnParsedLongFromStorage()
    {
        // Arrange
        _mockStorage.Setup(s => s.GetConfig("max_items"))
            .ReturnsAsync("10");

        // Act
        var value = AppAmbit.RemoteConfig.GetLong("max_items");

        // Assert
        Assert.Equal(10L, value);
    }

    [Fact]
    public void GetDouble_ShouldReturnParsedDoubleFromStorage()
    {
        // Arrange
        _mockStorage.Setup(s => s.GetConfig("discount_rate"))
            .ReturnsAsync("0.5");

        // Act
        var value = AppAmbit.RemoteConfig.GetDouble("discount_rate");

        // Assert
        Assert.Equal(0.5, value);
    }

    [Fact]
    public void GetBoolean_ShouldReturnParsedBooleanFromStorage()
    {
        // Arrange
        _mockStorage.Setup(s => s.GetConfig("is_new_ui"))
            .ReturnsAsync("true");

        // Act
        var value = AppAmbit.RemoteConfig.GetBoolean("is_new_ui");

        // Assert
        Assert.True(value);
    }
    
    private void ResetState()
    {
        // Access private static fields via reflection to reset RemoteConfig state
        var type = typeof(AppAmbit.RemoteConfig);
        
        var storageField = type.GetField("_storageService", BindingFlags.NonPublic | BindingFlags.Static);
        storageField?.SetValue(null, null);

        var apiField = type.GetField("_apiService", BindingFlags.NonPublic | BindingFlags.Static);
        apiField?.SetValue(null, null);

        var appInfoField = type.GetField("_appInfoService", BindingFlags.NonPublic | BindingFlags.Static);
        appInfoField?.SetValue(null, null);

        var isEnableField = type.GetField("_isEnable", BindingFlags.NonPublic | BindingFlags.Static);
        isEnableField?.SetValue(null, false);

        var isFetchCompletedField = type.GetField("_isFetchCompleted", BindingFlags.NonPublic | BindingFlags.Static);
        isFetchCompletedField?.SetValue(null, false);
    }

    [Fact]
    public async Task Fetch_Success_ShouldStoreConfigsToStorage()
    {
        // Arrange
        var mockResponse = new RemoteConfigResponse
        {
            Configs = new Dictionary<string, object>
            {
                { "welcome_msg", "Hello" }
            }
        };
        var apiResult = new ApiResult<RemoteConfigResponse>(mockResponse, ApiErrorType.None, null);

        _mockApiService
            .Setup(s => s.ExecuteRequest<RemoteConfigResponse>(It.IsAny<RemoteConfigEndpoint>()))
            .ReturnsAsync(apiResult);

        _mockAppInfoService.Setup(s => s.AppVersion).Returns("1.0.0");

        AppAmbit.RemoteConfig.Enable();

        // Act
        await AppAmbit.RemoteConfig.FetchAndStoreConfig();

        // Assert
        _mockApiService.Verify(s => s.ExecuteRequest<RemoteConfigResponse>(It.IsAny<RemoteConfigEndpoint>()), Times.Once);
        
        _mockStorage.Verify(s => s.AddConfigsAsync(It.Is<List<RemoteConfigEntity>>(l => 
            l.Any(x => x.Key == "welcome_msg" && x.Value == "Hello")
        )), Times.Once);
    }

    [Fact]
    public async Task Fetch_Failure_ShouldNotStoreConfigs()
    {
        // Arrange
        var apiResult = new ApiResult<RemoteConfigResponse>(null, ApiErrorType.NetworkUnavailable, null);

        _mockApiService
            .Setup(s => s.ExecuteRequest<RemoteConfigResponse>(It.IsAny<RemoteConfigEndpoint>()))
            .ReturnsAsync(apiResult);

        _mockAppInfoService.Setup(s => s.AppVersion).Returns("1.0.0");

        AppAmbit.RemoteConfig.Enable();

        // Act
        await AppAmbit.RemoteConfig.FetchAndStoreConfig();

        // Assert
        _mockStorage.Verify(s => s.AddConfigsAsync(It.IsAny<List<RemoteConfigEntity>>()), Times.Never);
    }
}
