namespace SqlGenerator
{
    public record ColumnDef(string Name, string Type, string Nullable, string Default, string Description);
}
