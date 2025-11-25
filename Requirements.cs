namespace InfoNode;

public class Requirements
{
    public static bool FamilyChecker(Document doc)
    {
        return new FilteredElementCollector(doc).OfClass(typeof(Family)).Cast<Family>().Any(f => f.Name == "InfoNode");

    }

    public static string ParameterChecker(Document doc)
    {
        var element = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_SpecialityEquipment).WhereElementIsNotElementType().FirstElement();

        if (element == null)
            return "InfoNode family not found";

        string[] paramNames = {
            "InfoNode_hostdata",
            "InfoNode_hostID",
            "InfoNode_hostname",
            "InfoNode_hosttag",
            "InfoNode_modname",
            "InfoNode_subs",
            "InfoNode_hostdata2"
        };

        var missing = paramNames.Where(name => element.LookupParameter(name) == null).ToList();

        if (missing.Any())
            return string.Join(", ", missing);

        return "";

    }

    public static string ModelChecker(Document doc)
    {
        // 1. Collect all loaded link model names from "model_name_drofus"
        var loadedModelNames = new List<string>();
        var linkInstances = new FilteredElementCollector(doc)
            .OfClass(typeof(RevitLinkInstance))
            .Cast<RevitLinkInstance>();

        foreach (var instance in linkInstances)
        {
            var linkedDoc = instance.GetLinkDocument();
            if (linkedDoc == null) continue;

            var projectInfo = linkedDoc.ProjectInformation;
            if (projectInfo == null) continue;

            var param = projectInfo.LookupParameter("model_name_drofus");
            if (param != null && param.HasValue && !string.IsNullOrWhiteSpace(param.AsString()))
            {
                loadedModelNames.Add(param.AsString());
            }
        }

        // 2. Collect all InfoNode family instances in the current doc
        var infoNodeInstances = new FilteredElementCollector(doc)
            .OfClass(typeof(FamilyInstance))
            .WhereElementIsNotElementType()
            .Cast<FamilyInstance>()
            .Where(fi => fi.Symbol.Family.Name == "InfoNode");

        // 3. For each InfoNode, check if InfoNode_modname is in loadedModelNames
        foreach (var instance in infoNodeInstances)
        {
            var modNameParam = instance.LookupParameter("InfoNode_modname");
            if (modNameParam == null) continue; // Or decide if this should be a failure

            var modName = modNameParam.AsString();
            
            // Skip check if modname is "Ingen data" (placeholder for missing data)
            if (modName == "Ingen data") continue;
            
            if (!string.IsNullOrWhiteSpace(modName) && !loadedModelNames.Contains(modName))
            {
                // Found an InfoNode referencing a model that is not loaded
                return modName;
            }
        }

        // All InfoNodes reference loaded models
        return "";
    }
}