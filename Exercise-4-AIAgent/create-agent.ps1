# Environment variables
$env:RESOURCE_GROUP_NAME = "<YOUR-RESOURCE-GROUP-NAME>"
$env:APP_SERVICE_NAME = "<YOUR-APP-SERVICE-NAME>"  # e.g. fashionassistant22zlpkjsmanlk
$env:AI_PROJECT_NAME = "<YOUR-AI-PROJECT-NAME>" # e.g. hol347aihproj22zlpkjsmanlk

# Ensure we have necessary parameters
if (-not $env:AI_PROJECT_NAME) {
    $env:AI_PROJECT_NAME = Read-Host "Enter your Azure ML project/workspace name"
}
if (-not $env:RESOURCE_GROUP_NAME) {
    $env:RESOURCE_GROUP_NAME = Read-Host "Enter your resource group name"
}

# Generate the Azure ML access token automatically using Azure CLI
try {
    Write-Host "Generating Azure ML access token..."
    $AZURE_AI_AGENTS_TOKEN = (az account get-access-token --resource 'https://ml.azure.com/' | ConvertFrom-Json).accessToken
    if (-not $AZURE_AI_AGENTS_TOKEN) {
        throw "Failed to generate token. Please ensure you're logged into Azure CLI."
    }
}
catch {
    Write-Error "Error generating Azure ML access token: $_"
    Write-Host "Make sure you're logged into Azure CLI with 'az login'"
    exit 1
}

# Get subscription ID if not provided
if (-not $env:SUBSCRIPTION_ID) {
    Write-Host "Getting current subscription ID..."
    try {
        $env:SUBSCRIPTION_ID = (az account show | ConvertFrom-Json).id
        Write-Host "Using subscription: $env:SUBSCRIPTION_ID"
    }
    catch {
        Write-Error "Error getting subscription ID: $_"
        $env:SUBSCRIPTION_ID = Read-Host "Enter your subscription ID"
    }
}

# Get the discovery_url to extract the hostname
try {
    Write-Host "Getting discovery_url from Azure ML workspace..."
    
    # First, let's check if the workspace exists by listing available workspaces
    Write-Host "Checking available ML workspaces in resource group $env:RESOURCE_GROUP_NAME..."
    $availableWorkspaces = az ml workspace list --resource-group $env:RESOURCE_GROUP_NAME --subscription $env:SUBSCRIPTION_ID --query "[].name" -o tsv
    
    if (-not $availableWorkspaces) {
        Write-Host "No ML workspaces found in resource group $env:RESOURCE_GROUP_NAME."
        Write-Host "Let's check all available workspaces in your subscription..."
        
        $allWorkspaces = az ml workspace list --subscription $env:SUBSCRIPTION_ID --query "[].{name:name, resourceGroup:resourceGroup}" -o json | ConvertFrom-Json
        
        if ($allWorkspaces -and $allWorkspaces.Count -gt 0) {
            Write-Host "Available ML workspaces in your subscription:"
            $allWorkspaces | ForEach-Object { Write-Host "- $($_.name) (resource group: $($_.resourceGroup))" }
            
            $useExisting = Read-Host "Would you like to use one of these workspaces? (y/n)"
            if ($useExisting -eq "y") {
                $env:AI_PROJECT_NAME = Read-Host "Enter the workspace name from the list above"
                $env:RESOURCE_GROUP_NAME = Read-Host "Enter the resource group for this workspace"
            } else {
                throw "No workspace selected. Please verify your project and resource group information."
            }
        } else {
            throw "No ML workspaces found in your subscription. Please create one first."
        }
    } else {
        Write-Host "Available ML workspaces in resource group $env:RESOURCE_GROUP_NAME:"
        $availableWorkspaces -split "`n" | ForEach-Object { Write-Host "- $_" }
        
        if ($availableWorkspaces -notcontains $env:AI_PROJECT_NAME) {
            $useExisting = Read-Host "Workspace '$env:AI_PROJECT_NAME' not found. Would you like to use one of the available workspaces? (y/n)"
            if ($useExisting -eq "y") {
                $env:AI_PROJECT_NAME = Read-Host "Enter the workspace name from the list above"
            } else {
                throw "Workspace '$env:AI_PROJECT_NAME' not found. Please verify your project name."
            }
        }
    }
    
    # Now try to get the discovery_url with the confirmed/updated workspace
    $discovery_url = (az ml workspace show -n $env:AI_PROJECT_NAME --subscription $env:SUBSCRIPTION_ID --resource-group $env:RESOURCE_GROUP_NAME --query discovery_url -o tsv)
    if (-not $discovery_url) {
        throw "Unable to get discovery_url. Please verify your workspace name, subscription, and resource group."
    }
    
    # Extract hostname from discovery_url
    # Remove both the leading "https://" and the trailing "/discovery"
    $hostname = $discovery_url -replace "^https://", "" -replace "/discovery$", ""
    Write-Host "Using hostname: $hostname"
      
    # Construct the full AZURE_AI_AGENTS_ENDPOINT
    $env:AZURE_AI_AGENTS_ENDPOINT = "https://$hostname/agents/v1.0/subscriptions/$env:SUBSCRIPTION_ID/resourceGroups/$env:RESOURCE_GROUP_NAME/providers/Microsoft.MachineLearningServices/workspaces/$env:AI_PROJECT_NAME"
    Write-Host "Generated endpoint: $env:AZURE_AI_AGENTS_ENDPOINT"
}
catch {
    Write-Error "Error generating Azure AI Agents endpoint: $_"
    Write-Host "Please make sure your project, resource group, and subscription information are correct."
    exit 1
}

# Read the swagger.json file
$swaggerPath = Join-Path $PSScriptRoot "swagger.json"
$swaggerContent = Get-Content -Path $swaggerPath -Raw | ConvertFrom-Json

# Update the server URL in the swagger content
$swaggerContent.servers[0].url = $env:FASHION_APP_SERVICE_URL

# Convert the swagger content back to JSON with proper formatting
$swaggerJson = $swaggerContent | ConvertTo-Json -Depth 100

$requestBody = @"
{
    "name": "FashionAssistant",
    "instructions": "You are an agent for a fashion store that sells clothing. You have the ability to view inventory, update the customer's shopping cart, and answer questions about the clothing items that are in the inventory and cart.",
    "model": "gpt-4o-mini",
    "tools": [
        {
            "type": "openapi",
            "openapi": {
                "name": "fashionassistant",
                "description": "This tool is used to interact with and manage an online fashion store. The tool can add or remove items from a shopping cart as well as view inventory.",
                "auth": {
                    "type": "anonymous"
                },
                "spec": $swaggerJson
            }
        }
    ]
}
"@

# Check if Fashion app service name is set
if (-not $env:APP_SERVICE_NAME) {
    $env:APP_SERVICE_NAME = Read-Host "Enter your Fashion App Service name (without .azurewebsites.net)"
}

# Construct the Fashion app service URL
$env:FASHION_APP_SERVICE_URL = "https://$($env:APP_SERVICE_NAME).azurewebsites.net"
Write-Host "Using Fashion App URL: $env:FASHION_APP_SERVICE_URL"

# Set up headers and call the API
$headers = @{
    "Authorization" = "Bearer $AZURE_AI_AGENTS_TOKEN"
    "Content-Type" = "application/json"
}

$uri = "$($env:AZURE_AI_AGENTS_ENDPOINT)/assistants?api-version=2024-12-01-preview"

try {
    # Get the raw response as a string
    $rawResponse = Invoke-WebRequest -Uri $uri -Method Post -Headers $headers -Body $requestBody | Select-Object -ExpandProperty Content
    
    # Extract just the ID using regex pattern
    if ($rawResponse -match '"id":\s*"([^"]+)"') {
        $agentId = $matches[1]
        "Agent ID: " + $agentId
    }
    else {
        # Fallback
        $agentId = ($rawResponse | ConvertFrom-Json).id
        "Agent ID: " + $agentId
    }
    
    # Set the agent ID as an app setting in the Azure App Service
    Write-Host "Setting the Agent ID as an app setting in the Azure App Service..."
    try {
        $result = az webapp config appsettings set -g $env:RESOURCE_GROUP_NAME -n $env:APP_SERVICE_NAME --settings "AzureAIAgent__AgentId=$agentId"
        Write-Host "Successfully created agent and updated app settings with Agent ID" -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to set app setting: $_"
        Write-Host "You may need to manually set the app setting with this command:" -ForegroundColor Yellow
        Write-Host "az webapp config appsettings set -g $env:RESOURCE_GROUP_NAME -n $env:APP_SERVICE_NAME --settings AzureAIAgent__AgentId=$agentId" -ForegroundColor Yellow
    }
}
catch {
    # Minimal error message
    Write-Error "Failed: $_"
    exit 1
}