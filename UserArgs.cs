using Microsoft.VisualBasic;

namespace InfoNode;

public class AssistantArgs
{
    [Description("Dry run"), ControlData(ToolTip = "")]
    public bool DryRun { get; set; } = false;
    [Description("Ignore host model name"), ControlData(ToolTip = "")]
    public bool IgnoreHostModelName { get; set; } = false;

    [Description("Host occurrence model name"), ControlData(ToolTip = "Sample tooltip")]
    public string ParamHostOccModelName { get; set; } = "parent_occurrence_id_occurrence_data_17_11_11_10";

    [Description("Host item data 1"), ControlData(ToolTip = "Sample tooltip")]
    public string ParamHostItemData1 { get; set; } = "parent_occurrence_id_article_id_dyn_article_13101110";

    [Description("Host item data 2"), ControlData(ToolTip = "Sample tooltip")]
    public string ParamHostItemData2 { get; set; } = "parent_occurrence_id_article_id_dyn_article_13101211";

    [Description("Select a phase for the infonodes"), ControlData(ToolTip = "Select the phase for InfoNode placement")]
    [RevitAutoFill(RevitAutoFillSource.Phases)]
    public string? RevitPhases { get; set; }

    [Description("Select a workset for the infonodes"), ControlData(ToolTip = "Select the workset for InfoNode placement")]
    [RevitAutoFill(RevitAutoFillSource.Worksets)]
    public string? RevitWorkset { get; set; }

    internal const string ParamHostOccTag = "parent_occurrence_id_classification_number";
}