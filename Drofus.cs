namespace InfoNode;

public class DrofusOccurrence
{
    public int SubOccId { get; set; }
    public string? SubIdNumber { get; set; }
    public string? SubItemName { get; set; }
    public int HostOccId { get; set; }
    public string? HostOccModname { get; set; }
    public string? HostItemName { get; set; }
    public string? HostOccDyn1 { get; set; }
    public string? HostItemDyn2 { get; set; }
    public string? HostOccTag { get; set; }
}

public class DrofusHost
{
    public int HostOccID { get; set; }
    public string? HostItemName { get; set; }
    public string? HostItemData1 { get; set; }
    public string? HostItemData2 { get; set; }
    public string? HostOccTag { get; set; }
    public string? HostOccModname { get; set; }
    public List<DrofusOccurrence> SubItems { get; set; } = new();
}