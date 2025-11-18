using dRofusClient.Filters;
using dRofusClient.Enums;
using dRofusClient.Occurrences;

namespace InfoNode;

public class InfoNodeHandlerCommand : IRevitExtension<AssistantArgs>
{
    public IExtensionResult Run(IRevitExtensionContext context, AssistantArgs args, CancellationToken cancellationToken)
    {
        var document = context.UIApplication.ActiveUIDocument?.Document;

        if (document is null)
            return Result.Text.Failed("Revit has no active model open");

        if (Requirements.FamilyChecker(document) == false)
            return Result.Text.Failed("Required InfoNode family does not exist in the model!");

        string parameterCheckerResult = Requirements.ParameterChecker(document);
        if (!string.IsNullOrEmpty(parameterCheckerResult))
            return Result.Text.Failed($"One or more required parameters missing from the project:\n{parameterCheckerResult}");

        string modelCheckerResult = Requirements.ModelChecker(document);
        if (!string.IsNullOrEmpty(modelCheckerResult))
            return Result.Text.Failed($"One or more relevant links not loaded:\n{modelCheckerResult}");


        var revitLinks = Revit.GetRevitLinks(document);

        var client = new dRofusClientFactory().Create(document);

        // Build query with filters: is_sub_occurrence = true AND host model name is in list of active links
        var querySubs = Query.List()
            .Select("Id", "article_id_number", "article_id_name", "parent_occurrence_id_id", args.ParamHostOccModelName, "parent_occurrence_id_article_id_name", args.ParamHostItemData1, args.ParamHostItemData2, "parent_occurrence_id_classification_number")
            .Filter(new FilterItem("is_sub_occurrence", Comparison.Eq, true))
            .Filter(new FilterItem(args.ParamHostOccModelName, Comparison.In, revitLinks));

        var allOccurrences = client.GetOccurrences(querySubs);

        // Convert the new client occurrences to the same format as the old DrofusOccurrence objects
        var subsInDrofus = allOccurrences.Select(occ => new DrofusOccurrence
        {
            SubOccId = occ.Id ?? 0,
            SubIdNumber = occ.AdditionalProperties?.GetValueOrDefault("article_id_number")?.ToString(),
            SubItemName = occ.AdditionalProperties?.GetValueOrDefault("article_id_name")?.ToString(),
            HostOccId = int.TryParse(occ.AdditionalProperties?.GetValueOrDefault("parent_occurrence_id_id")?.ToString(), out var hostId) ? hostId : 0,
            HostOccModname = occ.AdditionalProperties?.GetValueOrDefault(args.ParamHostOccModelName)?.ToString(),
            HostItemName = occ.AdditionalProperties?.GetValueOrDefault("parent_occurrence_id_article_id_name")?.ToString(),
            HostOccDyn1 = occ.AdditionalProperties?.GetValueOrDefault(args.ParamHostItemData1)?.ToString(),
            HostItemDyn2 = occ.AdditionalProperties?.GetValueOrDefault(args.ParamHostItemData2)?.ToString(),
            HostOccTag = occ.AdditionalProperties?.GetValueOrDefault("parent_occurrence_id_classification_number")?.ToString()
        }).ToList();

        // Now use the exact same syntax as the original commented code
        var hostsInDrofus = subsInDrofus.GroupBy(o => o.HostOccId).Select(group => new DrofusHost
        {
            HostOccID = group.Key,
            HostItemName = group.First().HostItemName,
            HostItemData1 = group.First().HostOccDyn1?.ToString(),
            HostItemData2 = group.First().HostItemDyn2?.ToString(),
            HostOccTag = group.First().HostOccTag,
            HostOccModname = group.First().HostOccModname?.ToString(),
            SubItems = group.ToList()
        }).ToList();




        var InstancesInRevit = Revit.CollectAllInstancesFromLinkedModels(document);

        // Clear hosts to avoid using stale data from previous runs
        Revit.ActualRevitHosts.Clear();

        foreach (var instance in InstancesInRevit)
        {
            int occurrenceId = instance.DrofusOccurrenceId;
            var matchingHost = hostsInDrofus.FirstOrDefault(h => h.HostOccID == occurrenceId);
            if (matchingHost != null)
            {
                Revit.ActualRevitHosts.Add(new Revit.ActualRevitHost
                {
                    DrofusOccurrenceId = instance.DrofusOccurrenceId,
                    Position = instance.Position,
                    ItemName = matchingHost.HostItemName,
                    ItemData1 = matchingHost.HostItemData1,
                    ItemData2 = matchingHost.HostItemData2,
                    Tag = matchingHost.HostOccTag,
                    Modname = matchingHost.HostOccModname,
                    SubItems = matchingHost.SubItems,
                });
            }
        }

        var activeRevitHosts = Revit.ActualRevitHosts;


        using (var tx = new Transaction(document, "Place or update Infonodes"))
        {
            tx.Start();

            foreach (var host in activeRevitHosts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Revit.PlaceOrUpdateInfoNode(document, host);
            }

            tx.Commit();


        }
        var createdIDs = new List<int>();
        var movedIDs = new List<int>();
        var duplicateIDs = new List<int>();
        var seenIds = new HashSet<int>();

        int createdCount = Revit.ActualRevitHosts.Count(h => h.Status == Revit.ActualHostStatus.Created);
        if (createdCount > 0)
        {
            foreach (var host in Revit.ActualRevitHosts)
            {
                if (host.Status == Revit.ActualHostStatus.Created)
                {
                    createdIDs.Add(host.DrofusOccurrenceId);
                }
            }
        }
        int movedCount = Revit.ActualRevitHosts.Count(h => h.Status == Revit.ActualHostStatus.Moved);
        if (movedCount > 0)
        {
            foreach (var host in Revit.ActualRevitHosts)
            {
                if (host.Status == Revit.ActualHostStatus.Moved && !movedIDs.Contains(host.DrofusOccurrenceId))
                {
                    movedIDs.Add(host.DrofusOccurrenceId);
                }
            }
        }
        int updatedCount = Revit.ActualRevitHosts.Count(h => h.Status == Revit.ActualHostStatus.Updated);
        var deletedCount = Revit.TheGreatPurge(document, activeRevitHosts);

        foreach (var host in Revit.ActualRevitHosts)
        {
            if (!seenIds.Add(host.DrofusOccurrenceId))
            {
                if (!duplicateIDs.Contains(host.DrofusOccurrenceId))
                    duplicateIDs.Add(host.DrofusOccurrenceId);
            }
        }

        string summarySuccess = ($"Success!\n\nCreated {createdCount} Infonodes for these hosts: ({String.Join(", ", createdIDs)})\n\nMoved {movedCount} Infonodes for these hosts: ({String.Join(", ", movedIDs)})\n\nUpdated {updatedCount} Infonodes\n\nDeleted {deletedCount} Infonodes");
        string summaryPartial = ($"Duplicates detected!\nThese duplicates exist in one of the linked models and confuse the script, triggering move ops for each run\nHere are the suspects: ({String.Join(", ", duplicateIDs)})\n\nCreated {createdCount} Infonodes for these hosts: ({String.Join(", ", createdIDs)})\nMoved {movedCount} Infonodes for these hosts: ({String.Join(", ", movedIDs)})\nUpdated {updatedCount} Infonodes\nDeleted {deletedCount} Infonodes");

        return duplicateIDs.Count > 0
            ? Result.Text.PartiallySucceeded(summaryPartial)
            : Result.Text.Succeeded(summarySuccess);
    
    }
}