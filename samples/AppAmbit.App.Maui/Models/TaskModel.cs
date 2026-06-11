using AppAmbit.Attributes;

namespace AppAmbitTestingApp.Models;

public class TaskModel
{
    public int Id { get; set; }
    public string? Title { get; set; }

    [DbColumn("is_completed")]
    public int IsCompleted { get; set; }

    public string? Priority { get; set; }

    [DbColumn("due_date")]
    public string? DueDate { get; set; }
}
