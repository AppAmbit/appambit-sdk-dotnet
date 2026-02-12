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
        AppAmbit.RemoteConfig.SetMinimumFetchIntervalInSeconds(0);
    }

    public void Dispose()
    {
        ResetState();
    }

    [Fact]
    public async Task Fetch_Success_ShouldStoreConfigsInMemory_AndReturnTrue()
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

        // Act
        var result = await AppAmbit.RemoteConfig.Fetch();

        // Assert
        Assert.True(result);
        _mockApiService.Verify(s => s.ExecuteRequest<RemoteConfigResponse>(It.IsAny<RemoteConfigEndpoint>()), Times.Once);
    }

    [Fact]
    public async Task Fetch_Failure_ShouldReturnFalse()
    {
        // Arrange
        var apiResult = new ApiResult<RemoteConfigResponse>(null, ApiErrorType.NetworkUnavailable, null);

        _mockApiService
            .Setup(s => s.ExecuteRequest<RemoteConfigResponse>(It.IsAny<RemoteConfigEndpoint>()))
            .ReturnsAsync(apiResult);

        _mockAppInfoService.Setup(s => s.AppVersion).Returns("1.0.0");

        // Act
        var result = await AppAmbit.RemoteConfig.Fetch();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task Activate_ShouldValidFetchedConfigToStorable()
    {
        // Arrange
        // 1. Mock fetch first to populate private _fetchedConfig
        var mockResponse = new RemoteConfigResponse
        {
            Configs = new Dictionary<string, object>
            {
                { "feature_enabled", true }
            }
        };
        var apiResult = new ApiResult<RemoteConfigResponse>(mockResponse, ApiErrorType.None, null);

        _mockApiService
            .Setup(s => s.ExecuteRequest<RemoteConfigResponse>(It.IsAny<RemoteConfigEndpoint>()))
            .ReturnsAsync(apiResult);
        
        await AppAmbit.RemoteConfig.Fetch();

        // Act
        var activated = await AppAmbit.RemoteConfig.Activate();

        // Assert
        Assert.True(activated);
        
        _mockStorage.Verify(s => s.AddConfigsAsync(It.Is<List<RemoteConfigEntity>>(l => 
            l.Count == 1 && 
            l[0].Key == "feature_enabled" && 
            l[0].Value == "True"
        )), Times.Once);
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
    public void GetString_ShouldFallbackToDefaults_IfStorageReturnsNull()
    {
        // Arrange
        _mockStorage.Setup(s => s.GetConfig("banner_text"))
            .ReturnsAsync((string?)null); // Use null for string

        AppAmbit.RemoteConfig.SetDefaults(new Dictionary<string, object>
        {
            { "banner_text", "Default Welcome" }
        });

        // Act
        var value = AppAmbit.RemoteConfig.GetString("banner_text");

        // Assert
        Assert.Equal("Default Welcome", value);
    }

    [Fact]
    public void GetInt_ShouldReturnParsedIntegerFromStorage()
    {
        // Arrange
        _mockStorage.Setup(s => s.GetConfig("max_items"))
            .ReturnsAsync("10");

        // Act
        var value = AppAmbit.RemoteConfig.GetInt("max_items");

        // Assert
        Assert.Equal(10, value);
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
        
        var defaultsField = type.GetField("_defaults", BindingFlags.NonPublic | BindingFlags.Static);
        defaultsField?.SetValue(null, new Dictionary<string, object>());

        var fetchedConfigField = type.GetField("_fetchedConfig", BindingFlags.NonPublic | BindingFlags.Static);
        fetchedConfigField?.SetValue(null, null);
        
        var storageField = type.GetField("_storageService", BindingFlags.NonPublic | BindingFlags.Static);
        storageField?.SetValue(null, null);

        var apiField = type.GetField("_apiService", BindingFlags.NonPublic | BindingFlags.Static);
        apiField?.SetValue(null, null);

        var appInfoField = type.GetField("_appInfoService", BindingFlags.NonPublic | BindingFlags.Static);
        appInfoField?.SetValue(null, null);

        var lastFetchTimeField = type.GetField("_lastFetchTime", BindingFlags.NonPublic | BindingFlags.Static);
        lastFetchTimeField?.SetValue(null, 0L);

        var intervalField = type.GetField("_minimumFetchIntervalInSeconds", BindingFlags.NonPublic | BindingFlags.Static);
        intervalField?.SetValue(null, 60L);
    }
}
