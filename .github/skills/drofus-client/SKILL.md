---
name: drofus-client
description: Guide for utilizing the dRofus API NuGet. Use this when asked to grab information from dRofus using the dRofus API.
---

dRofus is a REST API database used for storing and managing data related to building projects. It allows users to access and manipulate project data through a standardized API interface. We have created a NuGet package that allows us to easily interact with the dRofus API and retrieve information from it.

To utilize the dRofus API NuGet, follow these steps:

1. Repository: https://github.com/kristoffer-tungland/dRofusClient
2. Official README: https://github.com/kristoffer-tungland/dRofusClient/blob/master/README.md
3. Endpoints are defined as namespaces, depending on the type of data you want to access.

```csharp
using dRofusClient;
using dRofusClient.Rooms; // Namespace for room operations
using dRofusClient.Items; // Namespace for item operations
using dRofusClient.Occurrences;
```

4. Create a client instance. You can either provide login data manually:

```csharp
var connection = dRofusConnectionArgs.Create(
    baseUrl: "https://api.drofus.com",
    database: "DB",
    projectId: "ProjectId",
    username: "user",
    password: "password");

var client = new dRofusClientFactory().Create(connection);
```

Or let the NuGet package resolve required information from the active Revit document. In that case, it reads credentials from the registry and values such as database and project information from Revit Project Information. This is the recommended approach because it avoids hardcoding login data.

```csharp
var client = new dRofusClientFactory().Create(document);
```

5. Build a query. In the example below, attributes used in Select and Filter are internal dRofus attribute names.

```csharp
var queryRooms = Query.List()
    .Select("id", "name", "room_func_no", "drawing_no", "room_data_20101610", "room_data_20102210", "room_data_20102310", "room_data_21101010")
    .Filter(Filter.Eq("room_group_type_4_group_id_name", "Bygg 76"));
```

6. Execute the query and get results.

```csharp
var allRooms = client.GetRooms(queryRooms);
```

The response format typically looks like this:

```json
[
  {
    "id": 2310520,
    "project_id": 123,
    "room_id": 456,
    "article_id": null,
    "quantity": null,
    "comment": null,
    "article_id_name": "Dekontaminator, spyl, enkel med plass for des midler",
    "room_id_room_func_no": "02.01.0637"
  }
]
```

7. Other examples of queries:

```csharp
 var roomsQuery = Query.List()
                    .Select("id", args.RoomKeyDrofus, "name", "architect_no",
                        "room_data_20101610",
                        "room_data_20102210",
                        "room_data_20102310",
                        "room_data_21101010")
                    .Filter(Filter.In(args.RoomKeyDrofus, lookupKeys));

  rooms = client.GetRooms(roomsQuery).ToList();
```
