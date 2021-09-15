using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using HtmlAgilityPack;

namespace URLSummaryFn
{
    public static class URLSummary
    {
        public class OGStructure
        {
            public string BaseURL;
            public string Title;
            public string ImageURL;
            public string Summary;
        };
        private static string StrLeft(string sParam, int iLength)
        {
            string sResult = "";
            if (sParam.Length > 0)
            {
                if (iLength > sParam.Length)
                    iLength = sParam.Length;
                sResult = sParam.Substring(0, iLength);
            }
            return sResult;
        }

        [FunctionName("URLSummary")]
        [OpenApiOperation(operationId: "GetURLSummary", tags: new[] { "BaseURL" })]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiParameter(name: "BaseURL", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The URL to analyse")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "object", bodyType: typeof(OGStructure), Description = "OpenGraph details for this URL")]
        public static async Task<IActionResult> GetURLSummary(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {
                log.LogInformation("C# HTTP trigger function processed a request.");

                string sBaseURL = req.Query["BaseURL"];

                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);
                sBaseURL = sBaseURL ?? data?.BaseURL;

                if (!String.IsNullOrEmpty(sBaseURL))
                    return new OkObjectResult(await GetOGMetadata(sBaseURL));
                else
                    return new NotFoundResult();
            }
            catch (Exception e)
            {
            }
            return new BadRequestResult();
        }
        public static async Task<OGStructure> GetOGMetadata(string sURI)
        {
            OGStructure OGData = new OGStructure();
            OGData.BaseURL = sURI;
            Uri uri;
            Uri.TryCreate(sURI, UriKind.Absolute, out uri);
            HttpClient httpClient = new HttpClient();
            string html = await httpClient.GetStringAsync(uri);
            HtmlAgilityPack.HtmlDocument htmlDoc = new HtmlAgilityPack.HtmlDocument();
            htmlDoc.LoadHtml(html);
            HtmlNodeCollection metaTags = htmlDoc.DocumentNode.SelectNodes("//meta");
            if (metaTags is null)
            {
                // no data
            }
            else
            {
                foreach (HtmlNode tag in metaTags)
                {
                    string sName = tag.GetAttributeValue("property", "");
                    string sContent = tag.GetAttributeValue("content", "");
                    if (StrLeft(sName, 3) == "og:")
                    {
                        string sTag = sName.Substring(3, sName.Length - 3);
                        if (sName == "og:title")
                        {
                            OGData.Title = sContent;
                        }
                        else if (sName == "og:description")
                        {
                            OGData.Summary = sContent;
                        }
                        else if (sName == "og:image")
                        {
                            OGData.ImageURL = sContent;
                        }
                    }
                }
            }
            return OGData;
        }
    }
}

