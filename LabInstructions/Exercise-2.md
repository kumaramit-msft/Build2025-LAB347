# Exercise 2: Leverage Semantic Kernel to integrate with Azure OpenAI
In this exercise, you will use Semantic Kernel to integrate with Azure OpenAI. Using [Semantic Kernel](https://learn.microsoft.com/en-us/semantic-kernel/get-started/quick-start-guide?pivots=programming-language-csharp) can significantly enhance your AI application's capabilities by providing a unified interface, advanced orchestration, memory management, and robust error handling. It simplifies the integration process and ensures your application is scalable, secure, and resilient.

**Semantic Kernel client setup**
- Go to #region SemanticKernel in Exercise-2\devShopDNC\Controllers\ProductDetailsController.cs and view the kernel client settings
- AddAzureOpenAIChatCompletion method is used to configure Semantic Kernel to use Azure OpenAI's Chat Completion service with AOAI endpoint, deployment name and default Azure credentials to use System Managed to connect to Azure OpenAI. 

**Prompt Execution Settings**
- Using PromptExecutionSettings parameters, you can control various aspects of how AI generates the model responses making it easier to tailor the AI's output to your specific needs.
    - Temperature: Controls the randomness of the generated text. Lower values make the output more deterministic, while higher values make it more creative.
    - MaxTokens: Specifies the maximum number of tokens (words or parts of words) in the generated response.
    - TopP: Controls the cumulative probability for token selection. It is used for nucleus sampling, where the model considers only the tokens with the highest probabilities that add up to TopP.

**Maintain State**
- Azure Open AI responses are stateless. Maintaining a chat history is crucial for providing context to the AI model, enabling it to generate more coherent and contextually relevant responses.
- Go to #region ChatHistory in Exercise-2\devShopDNC\Controllers\ProductDetailsController.cs to view the prompts and responses are maintained 

**Azure Sign In**
- If you have already signed in to Azure, you can skip this step and move to deploy webapp
- Log into the provided Azure subscription in your environment using Azure CLI and on the Azure Portal using your credentials.
- Review the App Service Plan and the Azure Open AI service pre-provisioned in your subscription

### Deploy webapp to Azure App Service
- Right click on devshopDNC.csproj from Exercise 2 folder and select Open In Integrated Terminalaz login

  ![Context menu showing option to Open in integrated Terminal](./images/Exercise-1-terminal.png)

- To publish the web app, run the command in the opened terminal, run dotnet publish -c Release -o ./bin/Publish
- Right click on bin--> publish folder and select Deploy to webApp option
  
  
- Press Deploy
  
  ![Warning message asking if you're sure you want to deploy](./images/Exercise-1-deploy.png)

- Select the already existing webapp
  
  ![List of resources to select from](./images/Exercise-1-resource-select.png)
  
### Run the webapp
- Once deployed, click on the Browse button on the portal by going to the App Service web app view to view the web app
  
  ![Screenshot of website resource in Azure portal showing Browse option](./images/Exercise-1-browse-web.png)

  ![Image showing Homepage of Dev Shop application](./images/Exercise-1-webui.png.png)

### Enable Managed Identity

- The below step can be skipped if you completed Exercise 1

- System Identity has been already enabled for your web app. To view, search for Identity on Settings menu. Under System Assigned tab, the Status will be set to **ON**. 

 ![Identity settings in Azure Portal when viewing web app resource](./images/Exercise-1-SMI.png)

- As a next step, on Azure Open AI Resource, web app  "Role Assignment" has been set as Cognitive Services OpenAI Contributor.

### Connect to Azure Open AI

Now, the website is up and running. Lets connect with Azure OpenAI to get the Chat with AI Assistant integrated to the web app 

Add these appsettings to App Service web app.

- The below step can be skipped if you completed Exercise 1

- Go to Azure Open AI on the portal and open it in Azure AI Studio
  
 ![Azure Open AI resource in Azure portal](./images/Exercise-1-openai.png)

- Deploy the gpt-4o model by going to Deployments and select gpt-4o Chat completion model and click Confirm

 ![Azure AI Studio screenshot showing model Deployments](./images/Exercise-1-deploymodel.png)

- Give the deployment name and select deployment type as "Global Standard"

 ![Deploy model settings](./images/Exercise-1-gpt4o.png)

- Switch back to the App Service configuration blade. Add the environment variables DEPLOYMENT_NAME and ENDPOINT. For ENDPOINT value, use TargetUri and for DEPLOYMENT_NAME value, use deployment name retrieved from the above step 

 ![Environment variables section within Azure portal view of web app resource](./images/Exercise-1-envvar.png)

### Chat with AI Assistant
- Go to Clothing tab and select a product. 
- Click on **Chat with AI Assistant** and give an user message like "tell me more about the formal blazer"
- You should receive a chat response from the OpenAI model 

  
### Monitor the webapp
To monitor your web app, you can leverage the LogStream option under Monitoring section on the webapp portal view.

 ![Log stream experience for the web app in Azure portal](./images/Exercise-1-logs.png)
