using SQLite;

namespace AppAmbit.Models.App;

public class AppSecrets
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string? ConsumerId { get; set; }

    public string? AppId { get; set; }
    
    public string? DeviceId { get; set; } 
    
    public string? Token { get; set; }
    
    public string? SessionId { get; set; }
    
    public string? UserId { get; set; }
    
    public string? UserEmail { get; set; }

    public string? DeviceToken { get; set; }

    public bool? PushEnabled { get; set; }
}
