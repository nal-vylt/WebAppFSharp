module Infrastructure

open System
open Microsoft.Azure.Cosmos
open Domain
open System.Threading.Tasks
open System.Collections.Generic
open Azure
open Azure.Search.Documents
open Azure.Search.Documents.Models

let buildSearchQuery (request: SalesSearchRequest) =
    let filters = 
        [ 
            match request.employeeId with
            | Some id -> Some $"employeeId eq '%s{id}'"
            | None -> None

            match request.minRevenue with
            | Some revenue -> Some $"revenue ge %M{revenue}"
            | None -> None

            match request.maxRevenue with
            | Some revenue -> Some $"revenue le %M{revenue}"
            | None -> None
        ]
        |> List.choose id

    if filters.IsEmpty then
        "" // No filters
    else
        String.Join(" and ", filters)
            
module SaleRepository =
    let getContainer (cosmosClient: CosmosClient) =
        cosmosClient.GetDatabase("Sales").GetContainer("Sale")
    let createSaleAsync (cosmosClient: CosmosClient) (sale: Sale) : Task<Sale> =
        async {
            let container = getContainer(cosmosClient)
            let! saleItem =
                container.CreateItemAsync(sale, PartitionKey(sale.employeeId)) |> Async.AwaitTask
            return saleItem.Resource
        } |> Async.StartAsTask
    
    let getSaleByIdAsync (cosmosClient: CosmosClient)
        (employeeId: string)
        (id: Guid) : Task<Sale option> =
        async {
            try
                let container = getContainer(cosmosClient)
                let! itemResponse =
                    container.ReadItemAsync<Sale>(id.ToString(), PartitionKey(employeeId)) |> Async.AwaitTask
                return Some itemResponse.Resource
            with
            | _ -> return None
        } |> Async.StartAsTask

    let isExistedSaleAsync (cosmosClient: CosmosClient)
        (employeeId: string)
        (id: Guid) : Task<bool> =
        async {
            try
                let container = getContainer(cosmosClient)
                let! _ =
                    container.ReadItemAsync<Sale>(id.ToString(), PartitionKey(employeeId)) |> Async.AwaitTask
                return true
            with
            | _ -> return false
        } |> Async.StartAsTask
    
    let updateSaleAsync (cosmosClient: CosmosClient)
        (sale: Sale) : Task<Sale option> =
        async {
            try
                let container = getContainer(cosmosClient)
                let! itemResponse = container.UpsertItemAsync(sale, PartitionKey(sale.employeeId)) |> Async.AwaitTask
                return Some itemResponse.Resource
            with
            | _ -> return None
        } |> Async.StartAsTask

    let deleteSaleAsync (cosmosClient: CosmosClient)
        (employeeId: string)
        (id: Guid) : Task<bool> =
        async {
            try
                let container = getContainer(cosmosClient)
                let! _ =
                    container.DeleteItemAsync<User>(id.ToString(), PartitionKey(employeeId)) |> Async.AwaitTask
                return true
            with
            | _ -> return false
        } |> Async.StartAsTask
    
    let getAllSalesAsync (cosmosClient: CosmosClient)
        (limit: int option) : Task<List<Sale>> =
        async {
            let container = getContainer(cosmosClient)
            
            let query = 
                match limit with
                | Some count -> $"SELECT TOP %d{count} * FROM c"
                | None -> "SELECT * FROM c"

            let queryDefinition = QueryDefinition(query)

            let mutable sales = List<Sale>()
            let iterator = container.GetItemQueryIterator<Sale>(queryDefinition)

            while iterator.HasMoreResults do
                let! response = iterator.ReadNextAsync() |> Async.AwaitTask
                sales.AddRange(response.Resource)

            return sales
        } |> Async.StartAsTask
        
    let uploadSalesToSearchIndex (azureSearch: AzureSearchConfig) (sales: List<Sale>)=
        async {
            let credential = AzureKeyCredential(azureSearch.ApiKey)
            let searchClient =
                SearchClient(Uri(azureSearch.ServiceEndpoint), azureSearch.IndexName, credential)
            
            // Upload sales to the index
            let batch = IndexDocumentsBatch()
            for sale in sales do
                let uploadAction = IndexDocumentsAction.Upload sale
                batch.Actions.Add(uploadAction)
                
            // Upload the batch of sales to the search index
            try
                let! _ = searchClient.IndexDocumentsAsync(batch) |> Async.AwaitTask
                return ()
            with
            | ex -> raise(ex)
        } |> Async.StartAsTask
        
    let searchSales (azureSearch: AzureSearchConfig) (request: SalesSearchRequest) =
        async {
            let credential = AzureKeyCredential(azureSearch.ApiKey)
            let searchClient = SearchClient(Uri(azureSearch.ServiceEndpoint), azureSearch.IndexName, credential)

            let searchOptions = SearchOptions()
            let filterQuery = buildSearchQuery request
            if not (String.IsNullOrEmpty(filterQuery)) then
                searchOptions.Filter <- filterQuery

            let! searchResults = searchClient.SearchAsync<Sale>("*", searchOptions) |> Async.AwaitTask
            
            return 
                searchResults.Value.GetResults()
                |> Seq.map (_.Document)
        } |> Async.StartAsTask