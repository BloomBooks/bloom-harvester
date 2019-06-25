# bloom-harvester
# Bloom Library Harvester
## Basic Idea
This will be a process that can download books stored in Bloom Library and then upload products based on them so that they are available to users.
## Development
Open a command window. Navigate to the build folder under the root. Run getDependencies-windows.sh. This will download some dependencies to lib/dotnet/.
Then open BloomHarvester.sln in Visual Studio
If you want to view the logs or status in Azure Portal, you will obviously also need access to the specified resources.
## Setting Environment variables for runtime
* You will need to set some or all of the following Environment variables to store some keys:
** BloomHarvesterAzureAppInsightsKey{Prod|Test|Dev}
*** Initially, you can contact jeffrey_su@sil.org for the keys. Later on, you should ask alex_crum@sil.org to be added to the Azure instance, and then you can go to the Azure Portal, find the dev-harvestAppInsights/test-harvestAppInsights/harvestAppInsights resource, and copy the Instrumentation Key from there.
** BloomHarvesterParseAppId{Prod|Test|Dev}
*** You can find this from Parse DB dashboard. Or contact andrew_polk@sil.org.
** BloomHarvesterS3[Secret]Key{Prod|Test|Dev}.
*** Ask Alex Crum, alex_crum@sil.org. You can also ask jeffrey_su@sil.org or john_thomson@sil.org.
** BloomHarvesterUserName
*** harvester@bloomlibrary.org
** BloomHarvesterUserPassword{Prod|Test|Dev|Local}
*** Ask jeffrey_su@sil.org or john_thomson@sil.org.
## Azure
### Searching for specific log text
1. portal.azure.com
2. All resources.
3. Find the appropriate resource. (It should be an ApplicationInsights resource). Probably named "harvestAppInsights" or "dev-harvestAppInsights"
4. Open up the "Search" blade.
5. You can type the text you want to search and it'll look through all the traces, events, etc. for it
### Viewing metrics
1. portal.azure.com
2. All resources.
3. Find the appropriate resource. (It should be an ApplicationInsights resource). Probably named "harvestAppInsights" or "dev-harvestAppInsights"
4. Open up the "Metrics" blade.
5. Ensure the appropriate resource is selected. You can select a Metric such as "Events" and Aggregation=Sum to see the total number of events in each time period.
6. To get the count for a specific event, you can click "Add filter" and Property EventName = [the event name in question]
### Querying tables via SQL-like Syntax
1. portal.azure.com
2. All resources.
3. Find the appropriate resource. (It should be an ApplicationInsights resource). Probably named "harvestAppInsights" or "dev-harvestAppInsights"
4. Open up the "Servers" blade.
5. Click the "Servers Logs / analytics" button.
6. On the left, you will see a list of tables. Find the relevant table, hover over it, and press the eye icon in the right side.
7. You can write various SQL-like queries. The language is called "Kusto", you can search for the language documentation for that to perform more sophisticated queries.
