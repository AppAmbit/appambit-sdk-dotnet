using SQLite;

namespace AppAmbit.Models.Cms;

[Table("CmsEntries")]
public class CmsCacheEntity
{
    [PrimaryKey]
    public string ContentType { get; set; } = string.Empty;
    public string? JsonData { get; set; }
    public long LastSyncedAt { get; set; }
}
