# Exercise 2: Leverage Azure OpenAI SDK based Webjob
In this exercise, you will use Webjobs with OpenAI for generating a summary of product reviews.

**App Setup**
- This App uses Azure Storage Queue leveraging [Web-Queue-Worker](https://learn.microsoft.com/en-us/azure/architecture/guide/architecture-styles/web-queue-worker) architecture to generate AI summary for new reviews using Webjobs as background process.
- Go to #region publishreviewtoqueue in Exercise-1-IntegrateAOAI\devShopDNC\Controllers\ReviewController.cs and view how new review id is published to queue.
- Go to #region receivemessagefromqueue in Exercise-2-WebjobWithAOAI\ai-webjob-AOAI\Program.cs and view how Webjob will pop the item from queue.
- Go to #region openaichatclient in Exercise-2-WebjobWithAOAI\ai-webjob-AOAI\Program.cs and view how Azure OpenAI SDK is used in webjob to get updated AI summary for the product based on existing summary and new review.

**Azure Sign In**
- If you have already signed in to Azure, you can skip this step and move to deploy webapp
- Log into the provided Azure subscription in your environment using Azure CLI and on the Azure Portal using your credentials.
- Review the App Service Plan and the Azure Open AI service pre-provisioned in your subscription

### Deploy webapp to Azure App Service
- You can skip this step if you have already deployed the app from Exercise 1. Refer to the [Exercise 1 Lab Instructions](../Exercise-1.md#deploy-webapp-to-azure-app-service) for detailed steps on deploying the app.
  
### Run the webapp
- Once deployed, click on the Browse button on the portal by going to the App Service web app view to view the web app

  ![Screenshot of website resource in Azure portal showing Browse option](./images/LAB347-ex1-browse-web.png)

  ![Image showing Homepage of Dev Shop application](./images/LAB347-ex1-webui.png)

### Enable Managed Identity

- The below step can be skipped if you completed Exercise 1, you can check last item in this section for Storage account role assignment.

- System Identity has been already enabled for your web app. To view, search for Identity on Settings menu. Under System Assigned tab, the Status will be set to **ON**. 

 ![Identity settings in Azure Portal when viewing web app resource](./images/Exercise-1-SMI.png)

- As a next step, on Azure Open AI Resource, web app "Role Assignment" has been set as Cognitive Services OpenAI Contributor.
- **[NEW]** For Storage account, web app "Role Assignment" has been set as Storage Queue Data Contributor. This will be needed to publish and pop review data from Azure Storage Queue.

### Connect to Azure Open AI
- You can skip this step if you have already connected the app from Exercise 1. Refer to the [Exercise 1 Lab Instructions](../Exercise-1.md#connect-to-azure-open-ai) for detailed steps.

### Update Storage Queue details as App Settings (THIS STEP IS ALREADY DONE FOR YOU IN THIS LAB)
- Add STORAGE_ACCOUNT_NAME and QUEUE_NAME as this is required for choosing appropriate Azure Storage Queue by WebApp and Webjob for communication.
- Add WEBSITE_SKIP_RUNNING_KUDUAGENT as false, this is needed for running Webjobs.

 ![Image showing All App Settings](./images/LAB347-ex2-appsettings.png)

### Add OpenAI based Webjob to WebApp 
- Go to your app on Azure Portal and click option to "Add" under WebJobs.
 ![Add a new WebJob](./images/LAB347-ex2-webjob.png)

- [Download openai-webjob.zip](../Exercise-2-WebjobWithAOAI/ai-webjob-AOAI/openai-webjob.zip)

- Upload this webjob and choose to make it a Triggered (Scheduled one) with */5 * * * * * (run every 5 seconds) as the NCRONTAB expression.

 ![Add a new WebJob](./images/LAB347-ex2-webjobopenai.png)

 - Once WebJob is added, refresh the page:
 ![New WebJob on Portal](./images/LAB347-ex2-webjobopenaiadded.png)

 - You are all setup now.

### Run the entire setup

- Go to WebApp, and choose any product.
- See the current AI summary and avr rating of product.
  ![New WebJob on Portal](./images/LAB347-ex2-currentaisummary.png)

 - Add a review
  ![New WebJob on Portal](./images/LAB347-ex2-addnegativereview.png)

- Reload app page and see the udpated summary.
 ![New WebJob on Portal](./images/LAB347-ex2-updatedreview.png)

 - You an also check WebJob logs by clicking on logs link under WebJobs blade on Portal.