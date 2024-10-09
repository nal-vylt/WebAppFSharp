module Domain

open System

type Sale =
    {
      id: Guid
      employeeId: string
      revenue: decimal
      cost: decimal
      profit: decimal
    }
