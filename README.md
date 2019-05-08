# bloom-harvester
# Bloom Library Harvester
## Basic Idea
This will be a process that can download books stored in Bloom Library and then upload products based on them so that they are available to users.
## Development
Just open BloomHarvester.sln in Visual Studio
If you want to view the logs or status in Azure Portal, you will obviously also need access to the specified resources.
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
