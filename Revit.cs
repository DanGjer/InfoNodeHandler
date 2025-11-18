using Autodesk.Revit.DB.Structure;
namespace InfoNode;


public class Revit
{
    public static List<ActualRevitHost> ActualRevitHosts {get;set;} = new();

    public static List<string> GetRevitLinks (Document doc)
    {

        List<string> modnames = [];
        List<string> links = [];

        using var collector = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance));

        foreach (Element elem in collector)
        {


            if (elem is RevitLinkInstance linkInstance)
            {
                links.Add(linkInstance.Name);
            }
        }

        foreach (Element elem in collector)
        {
            
            if (elem is RevitLinkInstance linkInstance)
            {
                Document linkDoc = linkInstance.GetLinkDocument();
                if (linkDoc == null)
                {
                    continue;
                }
                var projectInfo = linkDoc.ProjectInformation;
                var modname = projectInfo.LookupParameter("model_name_drofus");

                if (modname is not null)
                {
                    var tempmodname = modname.AsString();
                    if (string.IsNullOrEmpty(tempmodname))
                    {
                        continue;
                    }
                    modnames.Add(tempmodname);
                }

            }

        }
        return modnames;
    }

    public static List<RevitInstance> CollectAllRevitInstances (Document doc)
    {
        var instances = new List<RevitInstance>();

        var elements = new FilteredElementCollector(doc).WhereElementIsNotElementType().OfClass(typeof(FamilyInstance)).ToElements();

        foreach (var element in elements)
        {
            var param = element.LookupParameter("drofus_occurrence_id");
            if (param == null || !param.HasValue)
                continue;
            
            int occId;

            if (param.StorageType == StorageType.Integer)
            {
                occId = param.AsInteger();
            }
            else if (param.StorageType == StorageType.String)
            {
                if (!int.TryParse(param.AsString(), out occId))
                    continue;
            }
            else
                continue;

            LocationPoint? location = element.Location as LocationPoint;
            if (location == null)
                continue;

            instances.Add(new RevitInstance
            {
                DrofusOccurrenceId = occId,
                Position = location.Point
            });
        }
        return instances;
    }

    public static List<RevitInstance> CollectAllInstancesFromLinkedModels (Document doc)
    {
        var allInstances = new List<RevitInstance>();

        var linkInstances = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>();

        foreach (var linkInstance in linkInstances)
        {
            var doclink = linkInstance.GetLinkDocument();
            if (doclink == null)
                continue;

            ProjectInfo projectInfo = doclink.ProjectInformation;
            if (projectInfo == null)
                continue;
            
            var modelNameParam = projectInfo.LookupParameter("model_name_drofus");
            if (modelNameParam == null)
                continue;
            
            string modelName = modelNameParam.AsString();
            if (string.IsNullOrWhiteSpace(modelName))
                continue;

            var occIds = CollectAllRevitInstances(doclink);
            allInstances.AddRange(occIds);
        }
        return allInstances;
    }

    public class RevitInstance
    {
        public int DrofusOccurrenceId {get; set;}
        public XYZ? Position {get; set;}
    }

    public class ActualRevitHost
    {
        public int DrofusOccurrenceId {get; set;}
        public XYZ? Position {get;set;}
        public string? ItemName {get;set;}
        public string? ItemData1 {get;set;}
        public string? ItemData2 {get;set;}
        public string? Tag {get;set;}
        public string? Modname { get; set; }
        public ActualHostStatus Status { get; set; }

        public List<DrofusOccurrence>? SubItems { get; set; }
        

    }

    public enum ActualHostStatus
    {
        Updated,
        Moved,
        Created
    }

    public static void PlaceOrUpdateInfoNode(Document doc, ActualRevitHost host)
    {
        double tolerance = 0.01;
        var symbol = new FilteredElementCollector(doc)
        .OfClass(typeof(FamilySymbol))
        .OfCategory(BuiltInCategory.OST_SpecialityEquipment)
        .Cast<FamilySymbol>()
        .FirstOrDefault(s => s.Name == "InfoNode");

        var existingInstance = new FilteredElementCollector(doc)
        .OfClass(typeof(FamilyInstance))
        .OfCategory(BuiltInCategory.OST_SpecialityEquipment)
        .Cast<FamilyInstance>()
        .FirstOrDefault(f =>
        {
            var idParam = f.LookupParameter("InfoNode_hostID");
            return idParam != null && idParam.AsString() == host.DrofusOccurrenceId.ToString();
        });

        string subItemSummary = host.SubItems != null ? string.Join(" | ", host.SubItems.Select(s => $"{s.SubOccId},{s.SubItemName}")) : string.Empty;

        

        if (existingInstance != null)
        {
            var location = existingInstance.Location as LocationPoint;
            if (location != null)
            {
                bool isSamePosition = location.Point.DistanceTo(host.Position) < tolerance;

                if (!isSamePosition)
                {
                    XYZ moveVector = host.Position - location.Point;
                    ElementTransformUtils.MoveElement(doc, existingInstance.Id, moveVector);
                    host.Status = ActualHostStatus.Moved;
                }
                else
                {
                    host.Status = ActualHostStatus.Updated;
                }
                SetStringParam(existingInstance, "InfoNode_hostID", host.DrofusOccurrenceId.ToString() ?? "Ingen data");
                SetStringParam(existingInstance, "InfoNode_hostname", host.ItemName ?? "Ingen data");
                SetStringParam(existingInstance, "InfoNode_hostdata", string.IsNullOrWhiteSpace(host.ItemData1) || host.ItemData1 == "0" ? "Ingen data" : host.ItemData1 ?? "Ingen data");
                SetStringParam(existingInstance, "InfoNode_hostdata2", host.ItemData2?.ToString() ?? "Ingen data");
                SetStringParam(existingInstance, "InfoNode_hosttag", host.Tag ?? "Ingen data");
                SetStringParam(existingInstance, "InfoNode_modname", host.Modname ?? "Ingen data");
                SetStringParam(existingInstance, "InfoNode_subs", subItemSummary);
                return;
            }
        }

        if (symbol == null)
            throw new Exception("InfoNode family symbol not found");

        if (!symbol.IsActive)
        {
            symbol.Activate();
            doc.Regenerate();
        }

        var newInstance = doc.Create.NewFamilyInstance(host.Position, symbol, StructuralType.NonStructural);
        SetStringParam(newInstance, "InfoNode_hostID", host.DrofusOccurrenceId.ToString() ?? "Ingen data");
        SetStringParam(newInstance, "InfoNode_hostname", host.ItemName ?? "Ingen data");
        SetStringParam(newInstance, "InfoNode_hostdata", string.IsNullOrWhiteSpace(host.ItemData1) || host.ItemData1 == "0" ? "Ingen data" : host.ItemData1 ?? "Ingen data");
        SetStringParam(newInstance, "InfoNode_hostdata2", host.ItemData2?.ToString() ?? "Ingen data");
        SetStringParam(newInstance, "InfoNode_hosttag", host.Tag ?? "Ingen data");
        SetStringParam(newInstance, "InfoNode_modname", host.Modname ?? "Ingen data");
        SetStringParam(newInstance, "InfoNode_subs", subItemSummary);

        host.Status = ActualHostStatus.Created;
    }

    public static void SetStringParam (Element element, string paramName, string value)
    {
        var param = element.LookupParameter(paramName);
        if (param != null && !param.IsReadOnly && param.StorageType == StorageType.String)
        {
            param.Set(value ?? "Ingen data");
        }

    }

    public static int TheGreatPurge(Document doc, List<ActualRevitHost> validHosts)
    {
        var validIDs = new HashSet<string>(validHosts.Select(h => h.DrofusOccurrenceId.ToString()));

        var collector = new FilteredElementCollector(doc)
            .OfClass(typeof(FamilyInstance))
            .OfCategory(BuiltInCategory.OST_SpecialityEquipment)
            .Cast<FamilyInstance>()
            .Where(f => f.Symbol.Name == "InfoNode")
            .ToList();

        var toDelete = new List<ElementId>();

        foreach (var instance in collector)
        {
            var idParam = instance.LookupParameter("InfoNode_hostID");

            if (idParam == null || string.IsNullOrWhiteSpace(idParam.AsString()))
            {
                toDelete.Add(instance.Id);
                continue;
            }

            string hostId = idParam.AsString();
            if (!validIDs.Contains(hostId))
            {
                toDelete.Add(instance.Id);
            }
        }

        int deletedCount = 0;

        using (var tx2 = new Transaction(doc, "The Great Purge"))
        {
            tx2.Start();

            foreach (var id in toDelete)
            {
                try
                {
                    doc.Delete(id);
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    Result.Text.Failed($"Failed to delete InfoNodes that were marked for deletion: {ex}");
                }
            }

            tx2.Commit();
        }

        return deletedCount;
    }


        
    
}