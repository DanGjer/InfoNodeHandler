using Microsoft.VisualBasic;

namespace InfoNode;

public class AssistantArgs
{
    [Description("Dry run"), ControlData(ToolTip = "")]
    public bool DryRun { get; set; } = false;

    [Description("Include local model"), ControlData(ToolTip = "Sample tooltip")]
    public bool IncludeLocalModel { get; set; } = false;

    [Description("Host occurrence model name"), ControlData(ToolTip = "Sample tooltip")]
    public string ParamHostOccModelName { get; set; } = "";

    [Description("Host item data 1"), ControlData(ToolTip = "Sample tooltip")]
    public string ParamHostItemData1 { get; set; } = "";

    [Description("Host item data 2"), ControlData(ToolTip = "Sample tooltip")]
    public string ParamHostItemData2 { get; set; } = "";

    [Description("Host item data 3"), ControlData(ToolTip = "Sample tooltip")]
    public string ParamHostItemData3 { get; set; } = "";

    [Description("Host occurrence data 1"), ControlData(ToolTip = "Sample tooltip")]
    public string ParamHostOccurrenceData1 { get; set; } = "";

    [Description("Host occurrence data 2"), ControlData(ToolTip = "Sample tooltip")]
    public string ParamHostOccurrenceData2 { get; set; } = "";

    [Description("Select a phase for the infonodes"), ControlData(ToolTip = "Select the phase for InfoNode placement")]
    [RevitAutoFill(RevitAutoFillSource.Phases)]
    public string? RevitPhases { get; set; }

    [Description("Select a workset for the infonodes"), ControlData(ToolTip = "Select the workset for InfoNode placement")]
    [RevitAutoFill(RevitAutoFillSource.Worksets)]
    public string? RevitWorkset { get; set; }

    internal const string ParamHostOccTag = "parent_occurrence_id_classification_number";

    [Description("Occurrence ID Parameter Names")]
    [ControlData(ToolTip = "Parameter names used to identify the occurrence ID on Revit elements")]
    public List<string> OccurrenceIdParameterNames { get; set; } = ["drofus_occurrence_id", "FOB_Database_ID.Forekomst_ID"];

    [Description("Sub item category filter"), ControlData(ToolTip = "If nothing is selected, then ALL subs will be looked up")]
    [ControlType(ControlType.ListBox), ControlSettings("CompactMode", "true")]
    [CustomRevitAutoFill(typeof(DrofusSubCategoryAutoFillCollector))]
    public List<string> SubFilter { get; set; } = [];
}