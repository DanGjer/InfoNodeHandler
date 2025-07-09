using Microsoft.VisualBasic;

namespace InfoNode;

public class AssistantArgs
{
    
    internal const string ParamSubOccID = "id";
    internal const string ParamSubItemNumber = "article_id_number";
    internal const string ParamSubItemName = "article_id_name";
    internal const string ParamHostOccID = "parent_occurrence_id_id";

    [Description("Host occurrence model name"), ControlData(ToolTip = "Sample tooltip")]
    public string ParamHostOccModelName { get; set; } = "parent_occurrence_id_occurrence_data_17_11_11_10";

    internal const string ParamHostItemName = "parent_occurrence_id_article_id_name";

    [Description("Host item data 1"), ControlData(ToolTip = "Sample tooltip")]
    public string ParamHostItemData1 { get; set; } = "parent_occurrence_id_article_id_dyn_article_13101110";

    [Description("Host item data 2"), ControlData(ToolTip = "Sample tooltip")]
    public string ParamHostItemData2 { get; set; } = "parent_occurrence_id_article_id_dyn_article_13101211";

    internal const string ParamHostOccTag = "parent_occurrence_id_classification_number";

    
}