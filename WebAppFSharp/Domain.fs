module Domain

open System
open Azure.Search.Documents.Indexes

type Sale = {
    [<SearchableField(IsFilterable = true, IsKey = true)>]
    id: Guid
    [<SearchableField(IsFilterable = true, IsSortable = true, IsFacetable = true)>]
    employeeId: string
    [<SearchableField(IsFilterable = true, IsSortable = true, IsFacetable = true)>]
    revenue: decimal
    [<SearchableField(IsFilterable = true, IsSortable = true, IsFacetable = true)>]
    cost: decimal
    [<SearchableField(IsFilterable = true, IsSortable = true, IsFacetable = true)>]
    profit: decimal
}

type AzureSearchConfig = {
    ServiceEndpoint: string
    ApiKey: string
    IndexName: string
}

type SalesSearchRequest = {
        employeeId: string option
        minRevenue: decimal option
        maxRevenue: decimal option
}  