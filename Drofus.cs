namespace InfoNode;

public class DrofusOccurrence
{
    public int SubOccId { get; set; }
    public string? SubIdNumber { get; set; }
    public string? SubItemName { get; set; }
    public int HostOccId { get; set; }
    public string? HostOccModname { get; set; }
    public string? RevitModname { get; set; }
    public string? HostItemName { get; set; }
    public string? HostData1 { get; set; }
    public string? HostData2 { get; set; }
    public string? HostData3 { get; set; }
    public string? HostData4 { get; set; }
    public string? HostData5 { get; set; }
    public string? HostOccTag { get; set; }
}

public class DrofusHost
{
    public int HostOccID { get; set; }
    public string? HostItemName { get; set; }
    public string? HostData1 { get; set; }
    public string? HostData2 { get; set; }
    public string? HostData3 { get; set; }
    public string? HostData4 { get; set; }
    public string? HostData5 { get; set; }
    public string? HostOccTag { get; set; }
    public string? HostOccModname { get; set; }
    public string? RevitModname { get; set; }
    public List<DrofusOccurrence> SubItems { get; set; } = new();
}