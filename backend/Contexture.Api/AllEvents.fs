module Contexture.Api.AllEvents

open Contexture.Api.Aggregates
open Contexture.Api.Infrastructure

  type AllEvents =
      | BoundedContexts of BoundedContext.Event
      | Domains of Domain.Event
      | Namespaces of Namespace.Event
      | NamespaceTemplates of NamespaceTemplate.Event
      | Collaboration of Collaboration.Event

      static member fromEnvelope(event: EventEnvelope) =
          match event.StreamKind with
          | kind when kind = StreamKind.Of<BoundedContext.Event>() ->
              event |> EventEnvelope.unbox |> EventEnvelope.map BoundedContexts |> Some
          | kind when kind = StreamKind.Of<Domain.Event>() ->
              event |> EventEnvelope.unbox |> EventEnvelope.map Domains |> Some
          | kind when kind = StreamKind.Of<Namespace.Event>() ->
              event |> EventEnvelope.unbox |> EventEnvelope.map Namespaces |> Some
          | kind when kind = StreamKind.Of<NamespaceTemplate.Event>() ->
              event |> EventEnvelope.unbox |> EventEnvelope.map NamespaceTemplates |> Some
          | kind when kind = StreamKind.Of<Collaboration.Event>() ->
              event |> EventEnvelope.unbox |> EventEnvelope.map Collaboration |> Some
          | _ -> None

      static member select<'E> (event: AllEvents) =
          match event with
          | e when typeof<'E> = typeof<AllEvents> -> e |> unbox<'E>
          | BoundedContexts e when typeof<'E> = typeof<BoundedContext.Event> -> e |> unbox<'E>
          | Domains e when typeof<'E> = typeof<Domain.Event>-> e |> unbox<'E>
          | Namespaces e when typeof<'E> = typeof<Namespace.Event>-> e |> unbox<'E>
          | NamespaceTemplates e when typeof<'E> = typeof<NamespaceTemplate.Event>-> e |> unbox<'E>
          | Collaboration e when typeof<'E> = typeof<Collaboration.Event>-> e |> unbox<'E>
          | other -> failwithf "Unable to match %s from %O" typeof<'E>.FullName other
