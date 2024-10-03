open System.Globalization

let myOne : double = 1.5
let myTwo = 2
let hello = "hello"
let letterA = 'a'
// mutable value
let mutable isEnabled = true
isEnabled <- false

// Function
let add x y = x + y
// Lambda expression
let add1 = fun x y -> x + y
let add2 x = fun y -> x + y

let add5 x =
    let five = 5
    x + five
    
let add5' x = add 5 x
let result = add5' 3
printfn $"%d{result}"

let operation number = (2. * (number + 3.)) ** 2.

// prefix
let add3 number = number + 3
let add3' number = (+) number 3
let add3'' = (+) 3
let time2 = (*) 2
let pow2 number = number ** 2.

// pipe operator
let operator number =
    number
    |> add3'
    |> time2

// Composite operator
// time2(add3'(number))
let operator' = 
    add3' >> time2


 