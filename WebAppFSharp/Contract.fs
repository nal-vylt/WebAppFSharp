module Contract

type Status =
    | Todo
    | Doing
    | Done

type ToDoList =
    { Name: string
      Description: string
      Status: Status
      PercentageDone: decimal }

type GetListsResponse = { lists: ToDoList [] }

let fromDomain (list: Domain.TodoList) =
    let mapStatus (s: Domain.Status) : Status =
        match s with
        | Domain.Status.Todo -> Todo
        | Domain.Status.Doing -> Doing
        | Domain.Status.Done -> Done

    { Name = list.Name
      Description = list.Description
      Status = list.Status |> mapStatus
      PercentageDone = list.PercentageDone }