#!/bin/bash

# Environment variables
export RESOURCE_GROUP_NAME="<YOUR-RESOURCE-GROUP-NAME>"
export APP_SERVICE_NAME="<YOUR-APP-SERVICE-NAME>"  # e.g. fashionassistant22zlpkjsmanlk
export AI_PROJECT_NAME="<YOUR-AI-PROJECT-NAME>" # e.g. hol347aihproj22zlpkjsmanlk

# Configure Azure CLI to allow dynamic installation of preview extensions
echo "Configuring Azure CLI to allow preview extensions..."
az config set extension.dynamic_install_allow_preview=true
az config set extension.use_dynamic_install=yes_without_prompt

# Ensure we have necessary parameters
if [ -z "$AI_PROJECT_NAME" ]; then
    read -p "Enter your Azure ML project/workspace name: " AI_PROJECT_NAME
    export AI_PROJECT_NAME
fi

if [ -z "$RESOURCE_GROUP_NAME" ]; then
    read -p "Enter your resource group name: " RESOURCE_GROUP_NAME
    export RESOURCE_GROUP_NAME
fi

# Generate the Azure ML access token automatically using Azure CLI
echo "Generating Azure ML access token..."
AZURE_AI_AGENTS_TOKEN=$(az account get-access-token --resource 'https://ml.azure.com/' --query accessToken -o tsv)

if [ -z "$AZURE_AI_AGENTS_TOKEN" ]; then
    echo "Error: Failed to generate token. Please ensure you're logged into Azure CLI."
    echo "Make sure you're logged into Azure CLI with 'az login'"
    exit 1
fi

# Get subscription ID if not provided
if [ -z "$SUBSCRIPTION_ID" ]; then
    echo "Getting current subscription ID..."
    SUBSCRIPTION_ID=$(az account show --query id -o tsv)
    
    if [ -z "$SUBSCRIPTION_ID" ]; then
        echo "Error: Failed to get subscription ID."
        read -p "Enter your subscription ID: " SUBSCRIPTION_ID
    else
        echo "Using subscription: $SUBSCRIPTION_ID"
    fi
    
    export SUBSCRIPTION_ID
fi

# Get the discovery_url to extract the hostname
echo "Getting discovery_url from Azure ML workspace..."

# Use the provided workspace name directly without verification
echo "Using Azure ML workspace: $AI_PROJECT_NAME"
echo "Running command: az ml workspace show -n \"$AI_PROJECT_NAME\" --subscription \"$SUBSCRIPTION_ID\" --resource-group \"$RESOURCE_GROUP_NAME\""

# Add timeout to az command to prevent hanging
WORKSPACE_INFO=$(timeout 30s az ml workspace show -n "$AI_PROJECT_NAME" --subscription "$SUBSCRIPTION_ID" --resource-group "$RESOURCE_GROUP_NAME" --output json 2>&1)
CMD_STATUS=$?

# Check if the command timed out or failed
if [ $CMD_STATUS -eq 124 ]; then
    echo "Error: Command timed out while retrieving workspace information."
    echo "Please check your network connection and Azure CLI installation."
    exit 1
elif [ $CMD_STATUS -ne 0 ]; then
    echo "Error retrieving workspace information:"
    echo "$WORKSPACE_INFO"
    echo "Please verify your workspace name, subscription ID, and resource group."
    exit 1
fi

echo "Workspace info successfully retrieved."

# Extract discovery_url from the workspace info
# Handle the case where output has text before the JSON data (common with experimental APIs)
echo "Extracting discovery_url from workspace info..."

# Try to extract JSON content by finding the first occurrence of '{'
JSON_CONTENT=$(echo "$WORKSPACE_INFO" | sed -n '/{/,$p')

if [ -z "$JSON_CONTENT" ]; then
    echo "Error: Failed to find JSON content in the response."
    echo "Raw workspace info: $WORKSPACE_INFO"
    exit 1
fi

# Try to parse and extract discovery_url
DISCOVERY_URL=$(echo "$JSON_CONTENT" | jq -r '.discovery_url' 2>/dev/null)

if [ -z "$DISCOVERY_URL" ] || [ "$DISCOVERY_URL" = "null" ]; then
    # If jq fails, try with a direct grep approach
    echo "Warning: JSON parsing failed, trying alternative extraction method..."
    DISCOVERY_URL=$(echo "$WORKSPACE_INFO" | grep -o '"discovery_url": *"[^"]*"' | sed 's/"discovery_url": *"\([^"]*\)"/\1/')
    
    if [ -z "$DISCOVERY_URL" ]; then
        echo "Error: Unable to extract discovery_url from workspace information."
        echo "Raw workspace info: $WORKSPACE_INFO"
        exit 1
    fi
fi

echo "Found discovery URL: $DISCOVERY_URL"

# Extract hostname from discovery_url
# Remove both the leading "https://" and the trailing "/discovery"
HOSTNAME=$(echo "$DISCOVERY_URL" | sed -e 's|^https://||' -e 's|/discovery$||')
echo "Using hostname: $HOSTNAME"

# Construct the full AZURE_AI_AGENTS_ENDPOINT
AZURE_AI_AGENTS_ENDPOINT="https://$HOSTNAME/agents/v1.0/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP_NAME/providers/Microsoft.MachineLearningServices/workspaces/$AI_PROJECT_NAME"
echo "Generated endpoint: $AZURE_AI_AGENTS_ENDPOINT"

# Check if Fashion app service name is set
if [ -z "$APP_SERVICE_NAME" ]; then
    read -p "Enter your Fashion App Service name (without .azurewebsites.net): " APP_SERVICE_NAME
    export APP_SERVICE_NAME
fi

# Construct the Fashion app service URL
APP_SERVICE_URL="https://$APP_SERVICE_NAME.azurewebsites.net"
echo "Using Fashion App URL: $APP_SERVICE_URL"

# Read the swagger.json file
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SWAGGER_PATH="$SCRIPT_DIR/swagger.json"

# Check if jq is installed
if ! command -v jq &> /dev/null; then
    echo "Error: jq is not installed. jq is required to update the swagger.json file."
    echo "Please install jq using your package manager (apt-get install jq, brew install jq, etc.)"
    exit 1
fi

# Check if the swagger file exists
if [ ! -f "$SWAGGER_PATH" ]; then
    echo "Error: swagger.json not found at $SWAGGER_PATH"
    echo "Make sure you're running this script from the correct directory."
    exit 1
fi

echo "Reading swagger.json from $SWAGGER_PATH"
# Update the server URL in the swagger content using jq
SWAGGER_CONTENT=$(cat "$SWAGGER_PATH")
SWAGGER_CONTENT=$(echo "$SWAGGER_CONTENT" | jq --arg url "$APP_SERVICE_URL" '.servers[0].url = $url')

if [ -z "$SWAGGER_CONTENT" ]; then
    echo "Error: Failed to update swagger content. Check if the file is valid JSON."
    exit 1
fi

# Create the request body
REQUEST_BODY=$(cat <<EOF
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
                "spec": $SWAGGER_CONTENT
            }
        }
    ]
}
EOF
)

# Set up headers and call the API
URI="$AZURE_AI_AGENTS_ENDPOINT/assistants?api-version=2024-12-01-preview"

echo "Calling API to create agent..."
RESPONSE=$(curl -s -X POST "$URI" \
    -H "Authorization: Bearer $AZURE_AI_AGENTS_TOKEN" \
    -H "Content-Type: application/json" \
    -d "$REQUEST_BODY")

# Extract the agent ID
AGENT_ID=$(echo "$RESPONSE" | grep -o '"id":\s*"[^"]*"' | sed 's/"id":\s*"\([^"]*\)"/\1/')

if [ -z "$AGENT_ID" ]; then
    # Try another approach with jq if available
    if command -v jq &> /dev/null; then
        AGENT_ID=$(echo "$RESPONSE" | jq -r '.id')
    fi
    
    if [ -z "$AGENT_ID" ]; then
        echo "Error: Failed to extract agent ID from response."
        echo "Response: $RESPONSE"
        exit 1
    fi
fi

echo "Agent ID: $AGENT_ID"

# Set the agent ID as an app setting in the Azure App Service
echo "Setting the Agent ID as an app setting in the Azure App Service..."
if az webapp config appsettings set -g "$RESOURCE_GROUP_NAME" -n "$APP_SERVICE_NAME" --settings "AzureAIAgent__AgentId=$AGENT_ID"; then
    echo -e "\033[0;32mSuccessfully created agent and updated app settings with Agent ID\033[0m"
else
    echo -e "\033[0;31mFailed to set app setting\033[0m"
    echo -e "\033[0;33mYou may need to manually set the app setting with this command:\033[0m"
    echo "az webapp config appsettings set -g $RESOURCE_GROUP_NAME -n $APP_SERVICE_NAME --settings AzureAIAgent__AgentId=$AGENT_ID"
fi
