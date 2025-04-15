using Azure.Identity;
using devShopDNC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Memory;
using NuGet.Protocol;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using System.Reflection.Metadata;
using System.Collections.Frozen;
using Microsoft.Identity.Client;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System.Text;
using Azure.Messaging;
using Microsoft.CodeAnalysis;
using NuGet.Configuration;
using System.Net;
using Microsoft.EntityFrameworkCore;



namespace devShopDNC.Controllers
{
    public class ProductDetailsController : Controller
    {
        
        ProductsDB ProductsDB = new ProductsDB();
        private readonly IConfiguration _configuration;
      
        // Session State
        public const string SessionKeyName = "_chatSessionHistory";
        public const string SessionKeyID = "_productID";
        public const string Data = "_dbData";

        // In Memory Cache
        private IMemoryCache _cache;

        public IActionResult ProductDetails(int ProductId)
        {

            List<ProductDetails> singleProductDetail = new List<ProductDetails>();

            singleProductDetail.Add(ProductsDB.GetProductDetails(ProductId));

            var product = ProductsDB.GetProductDetails(ProductId);
         
            string productID = ProductId.ToString();

            HttpContext.Session.SetString(SessionKeyID, productID);

            Console.WriteLine();
            Console.WriteLine("PRODUCT ID=" + productID);
            Console.WriteLine();

            return View(singleProductDetail);
        }

        [Route("/Chat")]
        public IActionResult Index()
        {
            return View("Chat");
        }

        private readonly ILogger<ProductDetailsController> _logger;

        public ProductDetailsController(ILogger<ProductDetailsController> logger, IConfiguration configuration, IMemoryCache cache)
        {
            _logger = logger;
            _configuration = configuration;
            _cache = cache;
        }
   

        [HttpPost]
        public async Task<IActionResult> GetResponse(string userMessage)
        {
            string product_ID;
            string productName = "";
            string productDescription = "";
            string ENDPOINT = _configuration["ENDPOINT"] ?? string.Empty;
            string DEPLOYMENT_NAME = _configuration["DEPLOYMENT_NAME"] ?? string.Empty;

            product_ID = HttpContext.Session.GetString(SessionKeyID) ?? string.Empty;
            if (!string.IsNullOrEmpty(product_ID))
            {
                var product = ProductsDB.GetProductDetails(int.Parse(product_ID));
                productName = product.ProductName;
                productDescription = product.ProductDescription;
            }

            string promptTemplate = $"{userMessage} Product: {productName}. Description: {productDescription}"; 

           #region SemanticKernel
            // Initialize Semantic Kernel
            IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.AddAzureOpenAIChatCompletion(
                deploymentName: DEPLOYMENT_NAME,
                credentials: new DefaultAzureCredential(),
                endpoint: ENDPOINT,
                modelId: "gpt-4o"
            );
            Kernel kernel = kernelBuilder.Build();

            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

            var promptSettings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0.7,
                MaxTokens = 300, // Adjust token limit as needed
                TopP = 0.95
            };
            #endregion

            #region ChatHistory
            ChatHistory history = [];
            history.AddSystemMessage("You are an AI assistant that helps people find concise information about products. Keep your responses brief and focus on key points.");
            history.AddUserMessage(promptTemplate);

            var response = await chatCompletionService.GetChatMessageContentAsync(
                history,
                executionSettings: promptSettings,
                kernel: kernel
            );
            #endregion
           
            string messageContent = FormatResponse(response.ToString());
            return Json(new { Response = messageContent });
        }


        // Helper function to format the response for better display
        private string FormatResponse(string messageContent)
        {
            messageContent = messageContent.Replace("**", "<strong>").Replace("**", "</strong>"); // Example: Markdown to HTML
            messageContent = messageContent.Replace("- ", "<li>").Replace("\n", "<br/>"); // Convert to list and line breaks

            return $"<p>{messageContent}</p>";
        }


    }
}
