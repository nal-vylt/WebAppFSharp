module Infrastructure

open System
open Microsoft.Azure.Cosmos
open Domain
open System.Threading.Tasks
open System.Collections.Generic

type CosmosDb =
    {
        ConnectionString: string
        ContainerId: string
        DatabaseId: string
    }
    
module SaleRepository =
    let getContainer (cosmosDb: CosmosDb) =
        let cosmosClient = new CosmosClient(cosmosDb.ConnectionString)
        cosmosClient.GetContainer(cosmosDb.DatabaseId, "Sale")
        
    let createSaleAsync (cosmosDb: CosmosDb) (sale: Sale) : Task<Sale> =
        async {
            let container = getContainer(cosmosDb)
            let! saleItem =
                container.CreateItemAsync(sale, PartitionKey(sale.employeeId)) |> Async.AwaitTask
                
            return saleItem.Resource
        } |> Async.StartAsTask
    
    let getSaleByIdAsync (cosmosDb: CosmosDb) (employeeId: string) (id: Guid) : Task<Sale option> =
        async {
            try
                let container = getContainer(cosmosDb)
                let! itemResponse =
                    container.ReadItemAsync<Sale>(id.ToString(), PartitionKey(employeeId)) |> Async.AwaitTask
                return Some itemResponse.Resource
            with
            | ex -> return None
        } |> Async.StartAsTask

    let isExistedSaleAsync (cosmosDb: CosmosDb) (employeeId: string) (id: Guid) : Task<bool> =
        async {
            try
                let container = getContainer(cosmosDb)
                let! _ =
                    container.ReadItemAsync<Sale>(id.ToString(), PartitionKey(employeeId)) |> Async.AwaitTask
                return true
            with
            | :? CosmosException as ex when ex.StatusCode = System.Net.HttpStatusCode.NotFound -> return false
            | ex -> return! Task.FromException<bool>(ex) |> Async.AwaitTask
        } |> Async.StartAsTask
    
    let updateSaleAsync (cosmosDb: CosmosDb) (sale: Sale) : Task<Sale option> =
        async {
            try
                let container = getContainer(cosmosDb)
                let! itemResponse = container.UpsertItemAsync(sale, PartitionKey(sale.employeeId)) |> Async.AwaitTask
                return Some itemResponse.Resource
            with
            | :? CosmosException as ex when ex.StatusCode = System.Net.HttpStatusCode.NotFound -> return None
        } |> Async.StartAsTask

    let deleteSaleAsync (cosmosDb: CosmosDb) (employeeId: string) (id: Guid) : Task<bool> =
        async {
            try
                let container = getContainer(cosmosDb)
                let! _ =
                    container.DeleteItemAsync<User>(id.ToString(), PartitionKey(employeeId)) |> Async.AwaitTask
                return true
            with
            | :? CosmosException as ex when ex.StatusCode = System.Net.HttpStatusCode.NotFound -> return false
            | ex -> return! Task.FromException<bool>(ex) |> Async.AwaitTask
        } |> Async.StartAsTask
    
    let getAllSalesAsync (cosmosDb: CosmosDb) (limit: int option) : Task<List<Sale>> =
        async {
            let container = getContainer(cosmosDb)
            
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