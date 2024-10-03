module Domain

type Status =
    | Todo
    | Doing
    | Done
    
type TodoList = {
    Name : string
    Description : string
    Status : Status
    PercentageDone : decimal    
}


type ListFetcher = string -> TodoList list