using dRofusClient.Filters;
using dRofusClient.Enums;
using dRofusClient.Occurrences;
using InfoNodeHandler;

namespace InfoNode;

public class InfoNodeHandlerCommand : IRevitExtension<AssistantArgs>
{
    private sealed class HostCollections
    {
        private readonly IReadOnlyList<Revit.ActualRevitHost> _hosts;

        public HostCollections(IReadOnlyList<Revit.ActualRevitHost> hosts)
        {
            _hosts = hosts;
        }

        public IEnumerable<Revit.ActualRevitHost> All => _hosts;
        public IEnumerable<Revit.ActualRevitHost> Created => _hosts.Where(h => h.Status == Revit.ActualHostStatus.Created);
        public IEnumerable<Revit.ActualRevitHost> Moved => _hosts.Where(h => h.Status == Revit.ActualHostStatus.Moved);
        public IEnumerable<Revit.ActualRevitHost> Updated => _hosts.Where(h => h.Status == Revit.ActualHostStatus.Updated);
    }

    private static ProgressUI.HostListItem ToHostListItem(Revit.ActualRevitHost host)
    {
        var subItemDetails = (host.SubItems ?? new List<DrofusOccurrence>())
            .Select(s => $"{(string.IsNullOrWhiteSpace(s.SubIdNumber) ? "-" : s.SubIdNumber)} | {(string.IsNullOrWhiteSpace(s.SubItemName) ? "(uten navn)" : s.SubItemName)}")
            .ToList();

        return new ProgressUI.HostListItem
        {
            DrofusOccurrenceId = host.DrofusOccurrenceId.ToString(),
            Name = host.ItemName ?? string.Empty,
            Mod = host.Modname ?? string.Empty,
            Tag = host.Tag ?? string.Empty,
            SubItems = subItemDetails.Count.ToString(),
            SubItemDetails = subItemDetails
        };
    }

    public IExtensionResult Run(IRevitExtensionContext context, AssistantArgs args, CancellationToken cancellationToken)
    {
        var document = context.UIApplication.ActiveUIDocument?.Document;
        var progressUI = new ProgressUI("InfoNoder");
        var uiEventHandler = new InfoNodeUiExternalEventHandler(progressUI.AppendLog);
        var uiExternalEvent = ExternalEvent.Create(uiEventHandler);
        progressUI.Show();

        try
        {
            progressUI.AppendLog("Starter InfoNode-script");

            if (document is null)
            {
                progressUI.AppendLog("Feil: Revit har ingen aktiv modell åpen.");
                return Result.Text.Failed("Revit har ingen aktiv modell åpen");
            }

            progressUI.SetHostActions(
                item =>
                {
                    if (!int.TryParse(item.DrofusOccurrenceId, out var hostId))
                    {
                        progressUI.AppendLog($"Velg mislyktes: ugyldig Infonode-ID '{item.DrofusOccurrenceId}'.");
                        return;
                    }

                    uiEventHandler.Queue(hostId, HostUiActionType.Select);
                    var request = uiExternalEvent.Raise();
                    if (request != ExternalEventRequest.Accepted)
                        progressUI.AppendLog($"Velg-forespørsel for Infonode {hostId} ble ikke akseptert ({request}).");
                },
                item =>
                {
                    if (!int.TryParse(item.DrofusOccurrenceId, out var hostId))
                    {
                        progressUI.AppendLog($"Gå til mislyktes: ugyldig Infonode-ID '{item.DrofusOccurrenceId}'.");
                        return;
                    }

                    uiEventHandler.Queue(hostId, HostUiActionType.JumpTo);
                    var request = uiExternalEvent.Raise();
                    if (request != ExternalEventRequest.Accepted)
                        progressUI.AppendLog($"Gå til-forespørsel for Infonode {hostId} ble ikke akseptert ({request}).");
                });

            progressUI.AppendLog("Sjekker krav...");

            bool pathcheckerResult = Requirements.PathChecker();
            if (!pathcheckerResult)
            {
                progressUI.AppendLog("Feil: Stier til nødvendige filer ikke funnet.");
                return Result.Text.Failed("Stier til nødvendig InfoNode-familie eller delt parameterfil ikke funnet.");
            }

            if (!Requirements.FamilyChecker(document))
            {
                progressUI.AppendLog("Familie ikke funnet, forsøker å importere...");
                if (!Requirements.FamilyImporter(document, out var importError))
                {
                    var reason = string.IsNullOrWhiteSpace(importError)
                        ? "Nødvendig InfoNode-familie kunne ikke lastes inn i modellen."
                        : $"Nødvendig InfoNode-familie kunne ikke lastes inn i modellen: {importError}";

                    progressUI.AppendLog("Feil: " + reason);
                    return Result.Text.Failed(reason);
                }

                if (!Requirements.FamilyChecker(document))
                {
                    progressUI.AppendLog("Feil: Familie fremdeles ikke funnet etter import.");
                    return Result.Text.Failed("Nødvendig InfoNode-familie finnes ikke i modellen etter forsøkt import.");
                }

                progressUI.AppendLog("Familie importert lyktes.");
            }

            progressUI.AppendLog("Sjekker parametere...");
            string parameterCheckerResult = Requirements.ParameterChecker(document);
            if (!string.IsNullOrEmpty(parameterCheckerResult))
            {
                progressUI.AppendLog("Feil: Parametere mangler.");
                return Result.Text.Failed($"En eller flere nødvendige parametere mangler fra prosjektet:\n{parameterCheckerResult}");
            }

            progressUI.AppendLog("Sjekker koblede modeller...");
            string modelCheckerResult = Requirements.ModelChecker(document, args.IgnoredRevitLinks);
            if (!string.IsNullOrEmpty(modelCheckerResult))
            {
                progressUI.AppendLog("Feil: Koblinger lastes ikke.");
                return Result.Text.Failed($"En eller flere relevante koblinger lastes ikke:\n{modelCheckerResult}");
            }

            progressUI.AppendLog("Krav OK");
            progressUI.AppendLog("Henter forekomster fra dRofus...");

            var client = new dRofusClientFactory().Create(document);

            // Build query with filters: is_sub_occurrence = true
            // Note: We no longer filter by host model name because IFC-imported elements won't have a model name in dRofus
            var querySubs = Query.List()
                .Select("Id", "article_id_number", "article_id_name", "parent_occurrence_id_id", args.ParamHostOccModelName, "parent_occurrence_id_article_id_name", args.ParamHostItemData1, args.ParamHostItemData2, "parent_occurrence_id_classification_number")
                .Filter(Filter.Eq("is_sub_occurrence", true));

            var allOccurrences = client.GetOccurrences(querySubs);
            progressUI.AppendLog($"Hentet {allOccurrences.Count()} forekomster.");

            progressUI.AppendLog("Kartlegger dRofus Infonode-data...");

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
                RevitModname = group.First().RevitModname,
                SubItems = group.ToList()
            }).ToList();

            progressUI.AppendLog("Samler instanser fra koblede modeller...");
            var instancesInRevit = Revit.CollectAllInstancesFromLinkedModels(document, args.OccurrenceIdParameterNames, args.IgnoredRevitLinks);
            progressUI.AppendLog($"Fant {instancesInRevit.Count} instanser i Revit.");

            progressUI.AppendLog("Samsvarer dRofus Infonoder med Revit-instanser...");

            // Clear hosts to avoid using stale data from previous runs
            Revit.ActualRevitHosts.Clear();

            foreach (var instance in instancesInRevit)
            {
                int occurrenceId = instance.DrofusOccurrenceId;
                var matchingHost = hostsInDrofus.FirstOrDefault(h => h.HostOccID == occurrenceId);

                if (matchingHost != null)
                {
                    // Set RevitModname from the instance so it can be used as fallback if dRofus modname is empty
                    matchingHost.RevitModname = instance.RevitModname;

                    Revit.ActualRevitHosts.Add(new Revit.ActualRevitHost
                    {
                        DrofusOccurrenceId = instance.DrofusOccurrenceId,
                        Position = instance.Position,
                        ItemName = matchingHost.HostItemName,
                        ItemData1 = matchingHost.HostItemData1,
                        ItemData2 = matchingHost.HostItemData2,
                        Tag = matchingHost.HostOccTag,
                        Modname = string.IsNullOrWhiteSpace(matchingHost.HostOccModname) ? matchingHost.RevitModname : matchingHost.HostOccModname,
                        SubItems = matchingHost.SubItems,
                    });
                }
            }

            var activeRevitHosts = Revit.ActualRevitHosts;
            var hostCollections = new HostCollections(activeRevitHosts);
            int totalHosts = activeRevitHosts.Count;
            progressUI.SetHostProviders(
                () => hostCollections.All.Select(ToHostListItem),
                () => hostCollections.Created.Select(ToHostListItem),
                () => hostCollections.Moved.Select(ToHostListItem),
                () => hostCollections.Updated.Select(ToHostListItem));
            progressUI.AppendLog($"Samsvarte {totalHosts} Infonoder. Starter plassering...");

            if (!args.DryRun)
            {
                using (var tx = new Transaction(document, "Plasser eller oppdater Infonoder"))
                {
                    tx.Start();

                    int processed = 0;
                    foreach (var host in activeRevitHosts)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Revit.PlaceOrUpdateInfoNode(document, host, args.DryRun, args.RevitPhases, args.RevitWorkset);
                        processed++;
                    }

                    tx.Commit();
                    progressUI.AppendLog($"Plasserte/oppdaterte {processed} Infonoder.");
                }
            }
            else
            {
                int processed = 0;
                foreach (var host in activeRevitHosts)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Revit.PlaceOrUpdateInfoNode(document, host, args.DryRun, args.RevitPhases, args.RevitWorkset);
                    processed++;
                }
                progressUI.AppendLog($"Torsjèk: evaluerte {processed} Infonoder.");
            }

            var createdIDs = new List<int>();
            var createdNames = new List<string>();
            var movedIDs = new List<int>();
            var movedNames = new List<string>();
            var duplicateIDs = new List<int>();
            var duplicateNames = new List<string>();
            var seenIds = new HashSet<int>();

            createdIDs.AddRange(hostCollections.Created.Select(h => h.DrofusOccurrenceId));
            createdNames.AddRange(hostCollections.Created.Select(h => h.ItemName ?? string.Empty));
            int createdCount = createdIDs.Count;

            movedIDs.AddRange(hostCollections.Moved.Select(h => h.DrofusOccurrenceId).Distinct());
            movedNames.AddRange(hostCollections.Moved.Select(h => h.ItemName ?? string.Empty).Distinct());
            int movedCount = movedIDs.Count;

            int updatedCount = hostCollections.Updated.Count();
            var deletedCount = Revit.TheGreatPurge(document, activeRevitHosts, args.DryRun);

            foreach (var host in hostCollections.All)
            {
                if (!seenIds.Add(host.DrofusOccurrenceId))
                {
                    if (!duplicateIDs.Contains(host.DrofusOccurrenceId))
                        duplicateIDs.Add(host.DrofusOccurrenceId);
                    if (!duplicateNames.Contains(host.ItemName ?? string.Empty))
                        duplicateNames.Add(host.ItemName ?? string.Empty);
                }
            }

            progressUI.AppendLog("Fullfører oppsummering...");

            string dryRunPrefix = args.DryRun ? "[TORSJØK]\n " : "";
            string summarySuccess = ($"{dryRunPrefix}Vellykket!\n\nOpprettet {createdCount} Infonoder for disse Infonodene: \n({String.Join(", ", createdIDs)})\nInfonodenavn: \n({String.Join(", ", createdNames)})\n\nFlyttet {movedCount} Infonoder for disse Infonodene: \n({String.Join(", ", movedIDs)})\nInfonodenavn: \n({String.Join(", ", movedNames)})\n\nOppdatert {updatedCount} Infonoder\n\nSlettet {deletedCount} Infonoder");
            string summaryPartial = ($"{dryRunPrefix}Duplikater oppdaget!\nDisse duplikatene eksisterer i en av de koblede modellene og forvirrer skriptet, og utløser move-operasjoner for hver kjøring\nDupliker-ID-er: \n({String.Join(", ", duplicateIDs)})\nDupliker-navn: \n({String.Join(", ", duplicateNames)})\n\nOpprettet {createdCount} Infonoder for disse Infonodene: \n({String.Join(", ", createdIDs)})\nInfonodenavn: \n({String.Join(", ", createdNames)})\n\nFlyttet {movedCount} Infonoder for disse Infonodene: \n({String.Join(", ", movedIDs)})\nInfonodenavn: \n({String.Join(", ", movedNames)})\n\nOppdatert {updatedCount} Infonoder\nSlettet {deletedCount} Infonoder");

            progressUI.AppendLog("Fullført.");

            return duplicateIDs.Count > 0
                ? Result.Text.PartiallySucceeded(summaryPartial)
                : Result.Text.Succeeded(summarySuccess);
        }
        finally
        {
            progressUI.Complete();
        }
    }
}