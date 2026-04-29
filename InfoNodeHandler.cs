using dRofusClient.Filters;
using dRofusClient.Enums;
using dRofusClient.Occurrences;
using InfoNodeHandler;

namespace InfoNode;

public class InfoNodeHandlerCommand : IRevitExtension<AssistantArgs>
{
    private sealed class OwnershipFailurePreprocessor : IFailuresPreprocessor
    {
        public bool HasOwnershipFailure { get; private set; }
        public string FailureDescription { get; private set; } = string.Empty;

        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            var failureMessages = failuresAccessor.GetFailureMessages();
            foreach (var failureMessage in failureMessages)
            {
                var description = failureMessage.GetDescriptionText() ?? string.Empty;
                if (!IsOwnershipConflictMessage(description))
                    continue;

                HasOwnershipFailure = true;
                if (string.IsNullOrWhiteSpace(FailureDescription))
                    FailureDescription = description;

                return FailureProcessingResult.ProceedWithRollBack;
            }

            return FailureProcessingResult.Continue;
        }
    }

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

    private static ProgressUI.HostListItem ToHostListItem(Revit.ActualRevitHost host, HashSet<int>? duplicateIds = null)
    {
        var subItemDetails = (host.SubItems ?? new List<DrofusOccurrence>())
            .Select(s => $"{(string.IsNullOrWhiteSpace(s.SubIdNumber) ? "-" : s.SubIdNumber)} | {(string.IsNullOrWhiteSpace(s.SubItemName) ? "(uten navn)" : s.SubItemName)}")
            .ToList();

        bool isDuplicate = duplicateIds != null
            && duplicateIds.Contains(host.DrofusOccurrenceId)
            && host.Status == Revit.ActualHostStatus.Moved;

        return new ProgressUI.HostListItem
        {
            DrofusOccurrenceId = host.DrofusOccurrenceId.ToString(),
            Name = host.ItemName ?? string.Empty,
            Mod = host.Modname ?? string.Empty,
            Tag = host.Tag ?? string.Empty,
            SubItems = subItemDetails.Count.ToString(),
            SubItemDetails = subItemDetails,
            IsDuplicate = isDuplicate,
            DuplicateWarning = isDuplicate
                ? "Dette elementet er et duplikat og forårsaker gjentatte move-operasjoner ved hver kjøring."
                : string.Empty
        };
    }

    private static bool IsOwnershipConflictMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        return message.Contains("owned by", StringComparison.OrdinalIgnoreCase)
            || message.Contains("another user", StringComparison.OrdinalIgnoreCase)
            || message.Contains("cannot edit", StringComparison.OrdinalIgnoreCase)
            || message.Contains("can't edit", StringComparison.OrdinalIgnoreCase)
            || message.Contains("permission", StringComparison.OrdinalIgnoreCase)
            || message.Contains("eies av", StringComparison.OrdinalIgnoreCase)
            || message.Contains("annen bruker", StringComparison.OrdinalIgnoreCase)
            || message.Contains("kan ikke redigere", StringComparison.OrdinalIgnoreCase)
            || message.Contains("tilgang", StringComparison.OrdinalIgnoreCase)
            || message.Contains("låst", StringComparison.OrdinalIgnoreCase)
            || message.Contains("laast", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildProgressBar(int current, int total, int width = 20)
    {
        if (total <= 0)
            return "[--------------------] 0%";

        var boundedCurrent = Math.Clamp(current, 0, total);
        var filled = (int)Math.Round((double)boundedCurrent / total * width, MidpointRounding.AwayFromZero);
        filled = Math.Clamp(filled, 0, width);
        var percent = (int)Math.Round((double)boundedCurrent / total * 100, MidpointRounding.AwayFromZero);

        return $"[{new string('#', filled)}{new string('-', width - filled)}] {percent}%";
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

            progressUI.AppendLog("Sjekker linkede modeller...");
            string modelCheckerResult = Requirements.ModelChecker(document, args.IgnoredRevitLinks);
            if (!string.IsNullOrEmpty(modelCheckerResult))
            {
                progressUI.AppendLog("Feil: Linker lastes ikke.");
                return Result.Text.Failed($"En eller flere relevante linker lastes ikke:\n{modelCheckerResult}");
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

            progressUI.AppendLog("Samler instanser fra linkede modeller...");
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

            var seenIds = new HashSet<int>();
            var duplicateIdSet = hostCollections.All
                .Where(h => !seenIds.Add(h.DrofusOccurrenceId))
                .Select(h => h.DrofusOccurrenceId)
                .ToHashSet();

            int totalHosts = activeRevitHosts.Count;
            progressUI.SetHostProviders(
                () => hostCollections.All.Select(h => ToHostListItem(h, duplicateIdSet)),
                () => hostCollections.Created.Select(h => ToHostListItem(h, duplicateIdSet)),
                () => hostCollections.Moved.Select(h => ToHostListItem(h, duplicateIdSet)),
                () => hostCollections.Updated.Select(h => ToHostListItem(h, duplicateIdSet)));
            progressUI.AppendLog($"Samsvarte {totalHosts} Infonoder. Starter plassering...");

            if (!args.DryRun)
            {
                using (var tx = new Transaction(document, "Plasser eller oppdater Infonoder"))
                {
                    var ownershipFailurePreprocessor = new OwnershipFailurePreprocessor();
                    var failureHandlingOptions = tx.GetFailureHandlingOptions();
                    failureHandlingOptions.SetFailuresPreprocessor(ownershipFailurePreprocessor);
                    failureHandlingOptions.SetClearAfterRollback(true);
                    tx.SetFailureHandlingOptions(failureHandlingOptions);

                    tx.Start();

                    int processed = 0;
                    foreach (var host in activeRevitHosts)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int current = processed + 1;
                        progressUI.UpdateProgressLine($"Processing {current}/{totalHosts} {BuildProgressBar(current, totalHosts)}");

                        try
                        {
                            Revit.PlaceOrUpdateInfoNode(document, host, args.DryRun, args.RevitPhases, args.RevitWorkset);
                            processed++;
                        }
                        catch (Exception ex) when (IsOwnershipConflictMessage(ex.Message))
                        {
                            tx.RollBack();
                            progressUI.AppendLog($"Feil: Kunne ikke redigere Infonode {host.DrofusOccurrenceId}.");
                            progressUI.AppendLog($"Årsak: {ex.Message}");
                            progressUI.AppendLog("Vennligst be kollega synkronisere eller be om redigeringstilgang.");
                            return Result.Text.Failed($"Eierskap blokkering: {ex.Message}\n\nVennligst be kollega synkronisere eller be om redigeringstilgang til Infonode {host.DrofusOccurrenceId}.");
                        }
                    }

                    var commitStatus = tx.Commit();
                    if (ownershipFailurePreprocessor.HasOwnershipFailure || commitStatus != TransactionStatus.Committed)
                    {
                        var reason = string.IsNullOrWhiteSpace(ownershipFailurePreprocessor.FailureDescription)
                            ? "Revit avbrøt transaksjonen på grunn av tilgang/eierskap."
                            : ownershipFailurePreprocessor.FailureDescription;

                        progressUI.AppendLog("Feil: Kunne ikke fullføre plassering/oppdatering fordi ett eller flere elementer er låst av annen bruker.");
                        progressUI.AppendLog($"Årsak: {reason}");
                        progressUI.AppendLog("Vennligst be kollega synkronisere eller be om redigeringstilgang.");
                        return Result.Text.Failed($"Eierskap blokkering: {reason}\n\nVennligst be kollega synkronisere eller be om redigeringstilgang.");
                    }

                    progressUI.AppendLog($"Plasserte/oppdaterte {processed} Infonoder.");
                }
            }
            else
            {
                int processed = 0;
                foreach (var host in activeRevitHosts)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int current = processed + 1;
                    progressUI.UpdateProgressLine($"Processing {current}/{totalHosts} {BuildProgressBar(current, totalHosts)}");

                    try
                    {
                        Revit.PlaceOrUpdateInfoNode(document, host, args.DryRun, args.RevitPhases, args.RevitWorkset);
                        processed++;
                    }
                    catch (Exception ex) when (IsOwnershipConflictMessage(ex.Message))
                    {
                        progressUI.AppendLog($"Feil: Kunne ikke redigere Infonode {host.DrofusOccurrenceId}.");
                        progressUI.AppendLog($"Årsak: {ex.Message}");
                        progressUI.AppendLog("Vennligst be kollega synkronisere eller be om redigeringstilgang.");
                        return Result.Text.Failed($"Eierskap blokkering: {ex.Message}\n\nVennligst be kollega synkronisere eller be om redigeringstilgang til Infonode {host.DrofusOccurrenceId}.");
                    }
                }
                progressUI.AppendLog($"Torsjèk: evaluerte {processed} Infonoder.");
            }

            var createdIDs = new List<int>();
            var createdNames = new List<string>();
            var movedIDs = new List<int>();
            var movedNames = new List<string>();
            var duplicateIDs = new List<int>();
            var duplicateNames = new List<string>();

            createdIDs.AddRange(hostCollections.Created.Select(h => h.DrofusOccurrenceId));
            createdNames.AddRange(hostCollections.Created.Select(h => h.ItemName ?? string.Empty));
            int createdCount = createdIDs.Count;

            movedIDs.AddRange(hostCollections.Moved.Select(h => h.DrofusOccurrenceId).Distinct());
            movedNames.AddRange(hostCollections.Moved.Select(h => h.ItemName ?? string.Empty).Distinct());
            int movedCount = movedIDs.Count;

            int updatedCount = hostCollections.Updated.Count();
            var deletedCount = Revit.TheGreatPurge(document, activeRevitHosts, args.DryRun);

            duplicateIDs.AddRange(duplicateIdSet.OrderBy(id => id));
            duplicateNames.AddRange(hostCollections.All
                .Where(h => duplicateIdSet.Contains(h.DrofusOccurrenceId))
                .Select(h => h.ItemName ?? string.Empty)
                .Distinct());

            progressUI.AppendLog("Fullfører oppsummering...");

            string dryRunPrefix = args.DryRun ? "[DRY RUN] " : "";
            string summarySuccess = ($"{dryRunPrefix}Vellykket!\n\nOpprettet {createdCount} Infonoder for disse hostene: \n({String.Join(", ", createdIDs)})\nHostnavn: \n({String.Join(", ", createdNames)})\n\nFlyttet {movedCount} Infonoder for disse hostene: \n({String.Join(", ", movedIDs)})\nHostnavn: \n({String.Join(", ", movedNames)})\n\nOppdatert {updatedCount} Infonoder\n\nSlettet {deletedCount} Infonoder");
            string summaryPartial = ($"{dryRunPrefix}Duplikater oppdaget!\nDisse duplikatene eksisterer i en av de linkede modellene og forvirrer skriptet, og utløser move-operasjoner for hver kjøring\nDupliker-ID-er: \n({String.Join(", ", duplicateIDs)})\nDupliker-navn: \n({String.Join(", ", duplicateNames)})\n\nOpprettet {createdCount} Infonoder for disse hostene: \n({String.Join(", ", createdIDs)})\nHostnavn: \n({String.Join(", ", createdNames)})\n\nFlyttet {movedCount} Infonoder for disse hostene: \n({String.Join(", ", movedIDs)})\nHostnavn: \n({String.Join(", ", movedNames)})\n\nOppdatert {updatedCount} Infonoder\nSlettet {deletedCount} Infonoder");

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