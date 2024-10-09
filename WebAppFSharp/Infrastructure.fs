module Infrastructure

open System
open Microsoft.Azure.Cosmos
open Domain
open System.Threading.Tasks
open System.Collections.Generic
    
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