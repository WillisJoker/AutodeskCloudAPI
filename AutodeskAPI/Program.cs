using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace AutodeskAPI
{
    public class AutodeskAuth
    {
        private string clientId = "trTOEGuigr6UnoRgAGAKOA5VJ4ryFCf6I2CtxqsqmCFugL1g";
        private string clientSecret = "QP2LIOmqSi2aLK0P20nfGdGgIHwugdwn9abqj5Mtdp5FOGAyG2ca3TAViAJ1wnWp";
        private string baseUrl = "https://developer.api.autodesk.com/";
        private string authRoute = "authentication/v1/authenticate";

        public async Task<string> GetAccessTokenAsync()
        {
            var client = new RestClient(baseUrl);
            var request = new RestRequest(authRoute, Method.Post);
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddParameter("client_id", clientId);
            request.AddParameter("client_secret", clientSecret);
            request.AddParameter("grant_type", "client_credentials");
            request.AddParameter("scope", "data:read");

            var response = await client.ExecuteAsync<AuthResponse>(request);
            return response.Data.access_token;
        }
    }

    public class AuthResponse
    {
        public string token_type { get; set; }
        public int expires_in { get; set; }
        public string access_token { get; set; }
    }

    public class AutodeskAPI
    {
        private string baseUrl = "https://developer.api.autodesk.com";
        private string accessToken;

        public AutodeskAPI(string token)
        {
            accessToken = token;
        }


        public async Task<JObject> GetProjectsAsync()
        {
            string route = "/project/v1/hubs";
            var client = new RestClient($"{baseUrl}");
            var request = new RestRequest(route, Method.Get);
            request.AddHeader("Authorization", $"Bearer {accessToken}");

            var response = await client.ExecuteAsync(request);
            return JObject.Parse(response.Content);
        }

        public async Task<JObject> GetProjectFilesAsync(string hubId)
        {
            string route = $"/project/v1/hubs/{hubId}/projects";
            var client = new RestClient(baseUrl);
            var request = new RestRequest(route, Method.Get);
            request.AddHeader("Authorization", $"Bearer {accessToken}");

            var response = await client.ExecuteAsync(request);
            return JObject.Parse(response.Content);
        }
        public async Task<JObject> GetModelDataAsync(string hubId, string projectId)
        {
            string route = $"/project/v1/hubs/{hubId}/projects/{projectId}";
            var client = new RestClient(baseUrl);
            var request = new RestRequest(route, Method.Get);
            request.AddHeader("Authorization", $"Bearer {accessToken}");

            var response = await client.ExecuteAsync(request);
            return JObject.Parse(response.Content);
        }

        public async Task<JObject> GetGolderContents(string projectId, string folderId)
        {
            string route = $"/data/v1/projects/{projectId}/folders/{folderId}/contents";
            var client = new RestClient(baseUrl);
            var request = new RestRequest(route, Method.Get);
            request.AddHeader("Authorization", $"Bearer {accessToken}");

            var response = await client.ExecuteAsync(request);
            return JObject.Parse(response.Content);
        }

        public async Task<JObject> GetItemContents(string projectId, string itemId)
        {
            string route = $"/data/v1/projects/{projectId}/items/{itemId}";
            var client = new RestClient(baseUrl);
            var request = new RestRequest(route, Method.Get);
            request.AddHeader("Authorization", $"Bearer {accessToken}");

            var response = await client.ExecuteAsync(request);
            return JObject.Parse(response.Content);
        }
    }

    public class RevitModelExtractor
    {
        public void ExtractWindows(JObject modelData)
        {
            var windows = modelData["data"]
                .SelectMany(item => item["attributes"]["components"])
                .Where(component => component["name"].ToString().Contains("Window"));

            foreach (var window in windows)
            {
                Console.WriteLine(window.ToString());
            }
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var auth = new AutodeskAuth();
            string token = await auth.GetAccessTokenAsync();
            var api = new AutodeskAPI(token);

            JObject projectsData = await api.GetProjectsAsync();
            string hubsId = "";
            foreach (var data in projectsData["data"])
            {
                hubsId = data["id"].ToString();
            }

            JObject projectFilesData = await api.GetProjectFilesAsync(hubsId);
            string projectId = "";
            foreach (var data in projectFilesData["data"])
            {
                if (data["attributes"]["name"].ToString().Equals("ACC_project"))
                {
                    projectId = data["id"].ToString();
                }
            }

            JObject modelData = await api.GetModelDataAsync(hubsId, projectId);

            string rootFolderId = modelData["data"]["relationships"]["rootFolder"]["data"]["id"].ToString();
            JObject folderData = await api.GetGolderContents(projectId, rootFolderId);

            string itemId = "";
            foreach (var data in folderData["data"])
            {
                if (data["attributes"]["name"].ToString().Equals("Project Files"))
                {
                    itemId = data["id"].ToString();
                }
            }


            JObject itemData = await api.GetGolderContents(projectId, itemId);
            string mepId = "";
            foreach (var data in itemData["data"])
            {
                if (data["attributes"]["name"].ToString().Equals("MEP"))
                {
                    mepId = data["id"].ToString();
                }
            }

            JObject rvtData = await api.GetGolderContents(projectId, mepId);

            foreach (var file in rvtData["data"])
            {
            

                var client = new RestClient("https://developer.api.autodesk.com/");
                var request = new RestRequest("/modelderivative/v2/designdata/job", Method.Post);
                request.AddHeader("Authorization", $"Bearer {token}");
                request.AddHeader("Content-Type", "application/json");


                // urn 需要去除結尾的 ==
                string urn = Base64Encode(itemId).TrimEnd('=');

                Console.WriteLine(urn);

                var body = new
                {
                    input = new
                    {
                        urn = urn,
                        compressedUrn = true,
                        rootFilename = "model1.rvt"
                    },
                    output = new
                    {
                        formats = new[]
                        {
                    new
                    {
                        type = "svf",
                        views = new[] { "2d", "3d" }
                    }
                }
                    }
                };

                string json = JsonSerializer.Serialize(body, new JsonSerializerOptions { WriteIndented = true });
                request.AddJsonBody(json);
               

                var response = client.Execute(request);

            }
        }
        static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }


    }
}
