namespace AppAmbit.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class DbColumnAttribute : Attribute
{
    public string Name { get; }
    public DbColumnAttribute(string name) => Name = name;
}
