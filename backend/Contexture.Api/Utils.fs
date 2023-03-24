namespace Contexture.Api.Infrastructure

open FsToolkit.ErrorHandling

module Tuple =
    let mapFst map (first, second) = (map first, second)
    let mapSnd map (first, second) = (first, map second)

module Option =
    let ofTryGet result =
        match result with
        | true, value -> Some value
        | _ -> None

module Async =

    let map mapper o =
        async {
            let! result = o
            return mapper result
        }

    let bindOption o =
        async {
            match o with
            | Some value ->
                let! v = value
                return Some v
            | None -> return None
        }

    let optionMap mapper o =
        async {
            let! bound = bindOption o
            return Option.map mapper bound
        }

type Agent<'T> = MailboxProcessor<'T>

type Clock = unit -> System.DateTimeOffset

module List =
    let maxOr defaultValue items =
        if List.isEmpty items then defaultValue else List.max items

// module NonEmptyList =
//     type NonEmptyList<'T> =
//         private Cons of 'T * NonEmptyList<'T> option
//     let single item = Cons (item, None)
//     let rec fromList (items: _ list) =
//         if items.IsEmpty then
//             None
//         else
//             Some (Cons (items.Head, fromList items.Tail))
//     let rec asList (Cons(head, tail)) =
//         match tail with
//         | None -> List.singleton head
//         | Some tail -> head :: asList tail
//         
//     let rec map mapper (Cons(head, tail)) =
//         Cons(
//             mapper head,
//             tail|> Option.map (map mapper)
//         )
//         
//         
type NonEmptyList<'T> =
    private Cons of 'T * 'T list
module NonEmptyList =
    let head (Cons(head, _)) = head
    let singleton item = Cons (item, List.empty)
    let fromList (items: _ list) =
        if items.IsEmpty then
            None
        else
            Some (Cons (items.Head, items.Tail))
    let asList (Cons(head, tail)) =
        head :: tail
        
    let map mapper (Cons(head, tail)) =
        Cons(
            mapper head,
            tail |> List.map mapper
        )
