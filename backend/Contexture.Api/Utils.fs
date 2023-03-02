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
