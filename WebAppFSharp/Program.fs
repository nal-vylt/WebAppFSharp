module WebAppFSharp

open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe
open System.Text.Json.Serialization
open Microsoft.Azure.Cosmos
open System.Text.Json
open Infrastructure
open Domain
open Application

let jsonOptions =
    let options = SystemTextJson.Serializer.DefaultOptions
    options.Converters.Add(JsonFSharpConverter(JsonUnionEncoding.FSharpLuLike))
    options

let getCosmosDb (configuration: IConfiguration) : CosmosDb =
    {
        ConnectionString = configuration.["CosmosDb:ConnectionString"]
        DatabaseId = configuration.["CosmosDb:DatabaseId"]
        ContainerId = "" 
    }
    
module RouteHandlers =
    let getSaleByIdHandler (employeeId, id) : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let configuration = ctx.GetService<IConfiguration>()
            let cosmosDb = getCosmosDb configuration
            
            task {
                let! result =
                    SaleRepository.getSaleByIdAsync cosmosDb employeeId id
                match result with
                    | Some s ->
                        ctx.SetStatusCode 200
                        return! json s next ctx
                    | None ->
                        return! RequestErrors.NOT_FOUND $"Sale not found: {string id}" next ctx   
            }
    
    type CreateSaleRequest =  { revenue: decimal; cost: decimal; profit: decimal }
    let createSaleHandler employeeId : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let configuration = ctx.GetService<IConfiguration>()
            let cosmosDb = getCosmosDb configuration
            
            task {
                try
                    let! request = ctx.BindJsonAsync<CreateSaleRequest>()
                    let sale = {
                        id = Guid.NewGuid()
                        employeeId = employeeId
                        revenue = request.revenue
                        cost = request.cost
                        profit = request.profit }
                    let! result =
                        SaleRepository.createSaleAsync cosmosDb sale
                    return! Successful.CREATED result next ctx
                with
                | :? JsonException ->
                    return! RequestErrors.BAD_REQUEST "Invalid request payload!" next ctx
                | ex ->
                    return! ServerErrors.INTERNAL_ERROR $"Unexpected error: %s{ex.Message}" next ctx
            }
    
    type UpdateSaleRequest =  { revenue: decimal; cost: decimal; profit: decimal }
    let updateSaleHandler (employeeId, id) : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let configuration = ctx.GetService<IConfiguration>()
            let cosmosDb = getCosmosDb configuration
            
            task {
                try
                    let! request = ctx.BindJsonAsync<UpdateSaleRequest>()
                    let! isExistedSale = SaleRepository.isExistedSaleAsync cosmosDb employeeId id
                    if isExistedSale = false then
                        return! RequestErrors.NOT_FOUND $"Sale not found: {string id}" next ctx  
                    else
                        let sale = {
                            id = id
                            employeeId = employeeId
                            revenue = request.revenue
                            cost = request.cost
                            profit = request.profit }
                        let! result = SaleRepository.updateSaleAsync cosmosDb sale
                        return! Successful.OK result next ctx
                with
                | :? JsonException ->
                    return! RequestErrors.BAD_REQUEST "Invalid request payload!" next ctx
                | ex ->
                    return! ServerErrors.INTERNAL_ERROR $"Unexpected error: %s{ex.Message}" next ctx
            }
            
    let deleteSaleHandler (employeeId, id) : HttpHandler =
         fun (next: HttpFunc) (ctx: HttpContext) ->
             let configuration = ctx.GetService<IConfiguration>()
             let cosmosDb = getCosmosDb configuration
             
             task {
                 try
                     let! result = SaleRepository.deleteSaleAsync cosmosDb employeeId id
                     if result = false then
                         return! RequestErrors.NOT_FOUND $"Sale not found: {string id}" next ctx
                     else return! Successful.OK result next ctx
                 with
                 | ex ->
                    return! ServerErrors.INTERNAL_ERROR $"Unexpected error: %s{ex.Message}" next ctx
             }
    
    let getSalesHandler : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let configuration = ctx.GetService<IConfiguration>()
            let cosmosDb = getCosmosDb configuration
            let limitQueryParam = ctx.TryGetQueryStringValue "limit"
            let limit =
                match limitQueryParam with
                | Some value ->
                    match Int32.TryParse(value) with
                    | (true, parsedLimit) -> Some parsedLimit
                    | _ -> None
                | None -> None
            
            task {
                let! result = SaleRepository.getAllSalesAsync cosmosDb limit |> Async.AwaitTask
                return! Successful.OK result next ctx
            }
    let exportSalesToExcelHandler : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let configuration = ctx.GetService<IConfiguration>()
            let cosmosDb = getCosmosDb configuration
            let excelTemplatePath = "template.xlsx"
            let limitQueryParam = ctx.TryGetQueryStringValue "limit"
            let limit =
                match limitQueryParam with
                | Some value ->
                    match Int32.TryParse(value) with
                    | (true, parsedLimit) -> Some parsedLimit
                    | _ -> None
                | None -> None
                
            task {
                try
                    // Fetch sales records from the database
                    let! sales = SaleRepository.getAllSalesAsync cosmosDb limit |> Async.AwaitTask

                    if sales.Count = 0 then
                        return! RequestErrors.NOT_FOUND "No sales found." next ctx
                    else
                        let! excelStream = ConvertToExcel.generateExcelFileAsync sales excelTemplatePath
                        
                        // Return file as a stream
                        // Set the response headers
                        ctx.Response.ContentType <- "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                        ctx.Response.Headers.Add("Content-Disposition", "attachment; filename=sales_report.xlsx")

                        // Copy the stream to the response body
                        do! excelStream.CopyToAsync(ctx.Response.Body) |> Async.AwaitTask
                        return! Successful.OK {||} next ctx
                with
                | ex ->
                    return! ServerErrors.INTERNAL_ERROR $"Unexpected error: %s{ex.Message}" next ctx
        }
             
    let webApp =
        choose [ route "/" >=> text "Hello Vy"
                 GET >=> routef "/employees/%s/sales/%O" getSaleByIdHandler
                 POST >=> routef "/employees/%s/sales" createSaleHandler
                 PUT >=> routef "/employees/%s/sales/%O" updateSaleHandler
                 DELETE >=> routef "/employees/%s/sales/%O" deleteSaleHandler
                 GET >=> route "/sales" >=> getSalesHandler
                 GET >=> route "/sales/export" >=> exportSalesToExcelHandler ]

let configureApp fetcher (app: IApplicationBuilder) = app.UseGiraffe(RouteHandlers.webApp)

let configureServices (services: IServiceCollection) =
    services
        .AddGiraffe()
        .AddSingleton<Json.ISerializer>(SystemTextJson.Serializer(jsonOptions))
    |> ignore

let configure (webHostBuilder: IWebHostBuilder) =
    webHostBuilder
        .Configure(configureApp)
        .ConfigureServices(configureServices)

[<EntryPoint>]
let main _ =

    let builder =
        Host
            .CreateDefaultBuilder()
            .ConfigureWebHostDefaults((configure) >> ignore)
            .Build()
    
    builder.Run()
    0