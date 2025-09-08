using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InfoNode;

public class Drofus
{

    
    public static string EndpointConstructor (string db, string projnumber, string server, string modname)
    {
        var args = new AssistantArgs();
        if (server == "db2.nosyko.no")
        {
            server = "https://api-no.drofus.com/api/";
        }
        else
        {
            throw new Exception("Server not found");
        }

        string endpoint = $"{server}{db}/{projnumber}/occurrences?$select={AssistantArgs.ParamSubOccID},{AssistantArgs.ParamSubItemNumber},{AssistantArgs.ParamSubItemName},{AssistantArgs.ParamHostOccID},{args.ParamHostOccModelName},{AssistantArgs.ParamHostItemName},{args.ParamHostItemData1},{args.ParamHostItemData2},{AssistantArgs.ParamHostOccTag}&$filter=is_sub_occurrence eq true and contains({args.ParamHostOccModelName},'{modname}')";
        return endpoint;
    }
    
    public static string GetUserFromRegistry ()
    {
        using RegistryKey Key =  Registry.CurrentUser.OpenSubKey(@"Software\ODBC\ODBC.INI\rofus");
        {
            if (Key is null)
            {
                throw new Exception("test");
            }
            var Username = Key.GetValue("Username");
            if (Username is null)
                throw new Exception("Username not found in registry");

            return (string)Username;
        }
    }

    public static string GetCreds (string user, string database)
    {
        string CredBuilder = $"drofus://{user}@{database}";

        var creds = Meziantou.Framework.Win32.CredentialManager.ReadCredential(CredBuilder) ?? throw new InvalidOperationException ("Credentials not found");
        if (creds.Password is null)
            throw new Exception("No credentials found");
        return creds.Password;

    }

    public static List<DrofusOccurrence>? DrofusAPI (Autodesk.Revit.DB.Document doc, string modname)
    {
        using (var client = new HttpClient())
        {
        try
        {
           
            (var drofusServer, var drofusProjNumber, var drofusDatabase) = Revit.GetUserProjectinfo(doc);
            var drofusUser = Drofus.GetUserFromRegistry();
            var drofusPass = Drofus.GetCreds(drofusUser, drofusServer);
            var endpoint = Drofus.EndpointConstructor(drofusDatabase, drofusProjNumber, drofusServer, modname);

            var request = new  HttpRequestMessage(HttpMethod.Get, endpoint);
            string credentials = $"{drofusUser}:{drofusPass}";
            var encodedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encodedCredentials);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = client.SendAsync(request).Result;

            if (response.IsSuccessStatusCode)
            {
                
                var responseText = response.Content.ReadAsStringAsync().Result;
                Console.WriteLine("API request OK!");
                Console.WriteLine($"Response content: {responseText}");
                var options = new JsonSerializerOptions
                {PropertyNameCaseInsensitive = true};

                var result = JsonSerializer.Deserialize<List<DrofusOccurrence>>(responseText, options);
                return result;
            }
            else
            {
                
                Console.WriteLine($"⚠️ dRofus API request failed (Status: {response.StatusCode}, Endpoint: {endpoint})");
                Console.WriteLine($"Response Content: {response.Content.ReadAsStringAsync().Result}");
                return null;
            }


        }
        catch (Exception ex)
        {
            
            Console.WriteLine($"❌ An error occurred while calling dRofus API : {ex.Message}");
            return null;
        }
        }
    }
}

public class DrofusOccurrence
{
    [JsonPropertyName("id")]
    public int SubOccId { get; set; }

    [JsonPropertyName("article_id_number")]
    public string? SubIdNumber { get; set; }

    [JsonPropertyName("article_id_name")]
    public string? SubItemName { get; set; }

    [JsonPropertyName("parent_occurrence_id_id")]
    public int HostOccId { get; set; }

    [JsonPropertyName("parent_occurrence_id_occurrence_data_17_11_11_10")]
    public string? HostOccModname { get; set; }

    [JsonPropertyName("parent_occurrence_id_article_id_name")]
    public string? HostItemName { get; set; }

    [JsonPropertyName("parent_occurrence_id_article_id_dyn_article_13101110")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? HostOccDyn1 { get; set; }

    [JsonPropertyName("parent_occurrence_id_article_id_dyn_article_13101211")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? HostItemDyn2 { get; set; }

    [JsonPropertyName("parent_occurrence_id_classification_number")]
    public string? HostOccTag { get; set; }
}

public class DrofusHost
{
    public int HostOccID {get; set;}
    public string? HostItemName {get; set;}
    public string? HostItemData1 {get; set;}
    public string? HostItemData2 {get; set;}
    public string? HostOccTag {get; set;}
    public string? HostOccModname {get; set;}
    public List<DrofusOccurrence> SubItems {get; set;} = new();
}

public class FlexibleStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Number:
                return reader.GetDouble().ToString(); // or GetInt32() if you expect only integers
            case JsonTokenType.Null:
                return null;
            default:
                throw new JsonException($"Unexpected token parsing string. Expected String or Number, got {reader.TokenType}.");
        }
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}