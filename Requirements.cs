namespace InfoNode;

public class Requirements
{
    private const string InfoNodeFamilyPath = @"O:\A005000\A008170\EL\Utvikling\dRofusSubs_RETIRED\InfoNode.rfa";
    private const string InfoNodeSharedParamPath = @"O:\A005000\A008170\EL\Utvikling\dRofusSubs_RETIRED\Shared params.txt";

    public static bool PathChecker()
    {
        return System.IO.File.Exists(InfoNodeFamilyPath) && System.IO.File.Exists(InfoNodeSharedParamPath);
    }

    public static bool FamilyImporter(Document doc, out string? error)
    {
        error = null;

        if (!System.IO.File.Exists(InfoNodeFamilyPath))
        {
            error = $"InfoNode family file not found at {InfoNodeFamilyPath}";
            return false;
        }

        using (var tx = new Transaction(doc, "Load InfoNode family"))
        {
            tx.Start();

            try
            {
                if (doc.LoadFamily(InfoNodeFamilyPath, out var family) && family != null)
                {
                    tx.Commit();
                    return true;
                }

                tx.RollBack();
                error = "Revit returned false while loading the InfoNode family.";
                return false;
            }
            catch (Exception ex)
            {
                if (tx.GetStatus() == TransactionStatus.Started)
                    tx.RollBack();

                error = $"Exception while loading InfoNode family: {ex.Message}";
                return false;
            }
        }
    }

    public static void ParameterImporter(Document doc)
    {
        // Capture the current shared parameter file path from Revit (robust: use DefinitionFile when available)
        var app = doc.Application;
        if (app == null)
            return;

        var sharedParamDefFile = app.OpenSharedParameterFile();
        string currentSharedParamFilePath = sharedParamDefFile?.Filename ?? string.Empty;

        // Verify the InfoNode shared param file exists before attempting to use it
        if (!System.IO.File.Exists(InfoNodeSharedParamPath))
            return;

        // Set the shared parameter file to the InfoNode shared parameters file
        app.SharedParametersFilename = InfoNodeSharedParamPath;

        // Open the shared parameter file and bind all parameters to the document
        try
        {
            var infoNodeParamDefFile = app.OpenSharedParameterFile();
            if (infoNodeParamDefFile == null)
            {
                return;
            }

            // Create a category set for binding (OST_SpecialityEquipment is where InfoNode instances live)
            var categorySet = new CategorySet();
            categorySet.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_SpecialityEquipment));

            // Iterate through all groups and parameters in the shared parameter file
            foreach (DefinitionGroup group in infoNodeParamDefFile.Groups)
            {
                foreach (Definition paramDef in group.Definitions)
                {
                    // Check if parameter is already bound
                    if (doc.ParameterBindings.Contains(paramDef))
                        continue;

                    // Create an instance binding and insert the parameter into the document
                    var instanceBinding = app.Create.NewInstanceBinding(categorySet);
                    doc.ParameterBindings.Insert(paramDef, instanceBinding);
                }
            }

            // Restore the original shared parameter file path
            if (!string.IsNullOrWhiteSpace(currentSharedParamFilePath))
                app.SharedParametersFilename = currentSharedParamFilePath;
        }
        catch (Exception)
        {
            // Log or handle import failure silently; ParameterChecker will detect if params still missing
        }
    }
    public static bool FamilyChecker(Document doc)
    {
        return new FilteredElementCollector(doc).OfClass(typeof(Family)).Cast<Family>().Any(f => f.Name == "InfoNode");

    }

    public static string ParameterChecker(Document doc)
    {
        string[] paramNames = {
            "InfoNode_hostdata",
            "InfoNode_hostID",
            "InfoNode_hostname",
            "InfoNode_hosttag",
            "InfoNode_modname",
            "InfoNode_subs",
            "InfoNode_hostdata2"
        };

            // Get the InfoNode family
            var infoNodeFamily = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .FirstOrDefault(f => f.Name == "InfoNode");

            if (infoNodeFamily == null)
                return "InfoNode family not found";

            using (var tx = new Transaction(doc, "Check InfoNode parameters"))
            {
                tx.Start();

                try
                {
                    // Get a default family symbol
                    var symbol = infoNodeFamily.GetFamilySymbolIds()
                        .Select(id => doc.GetElement(id))
                        .OfType<FamilySymbol>()
                        .FirstOrDefault();

                    if (symbol == null)
                    {
                        tx.RollBack();
                        return "No family symbol found in InfoNode family";
                    }

                    if (!symbol.IsActive)
                        symbol.Activate();

                    // Place temporary instance at a high Z coordinate (1000 feet up, well out of the way)
                    var tempPoint = new XYZ(0, 0, 1000);
                    var tempInstance = doc.Create.NewFamilyInstance(tempPoint, symbol, StructuralType.NonStructural);

                    // Check for required parameters
                    var missing = paramNames.Where(name => tempInstance.LookupParameter(name) == null).ToList();

                    if (missing.Any())
                    {
                        // Attempt to import missing parameters, then re-check
                        ParameterImporter(doc);

                        // Re-check against the temporary instance after import
                        missing = paramNames.Where(name => tempInstance.LookupParameter(name) == null).ToList();
                    }

                    // Delete the temporary instance
                    doc.Delete(tempInstance.Id);

                    tx.Commit();

                    if (missing.Any())
                        return string.Join(", ", missing);

                    return "";
                }
                catch (Exception ex)
                {
                    if (tx.GetStatus() == TransactionStatus.Started)
                        tx.RollBack();

                    return $"Error checking parameters: {ex.Message}";
                }
            }
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