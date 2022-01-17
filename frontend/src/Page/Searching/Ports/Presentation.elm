port module Page.Searching.Ports.Presentation exposing (SearchResultPresentation(..), SunburstPresentation(..), presentationLoaded, savePresentation)

import Components.BoundedContextsOfDomain as BoundedContext


type SunburstPresentation
    = Filtered
    | Highlighted


type SearchResultPresentation
    = Textual BoundedContext.Presentation
    | Sunburst SunburstPresentation
    | Hierarchical


savePresentation : SearchResultPresentation -> Cmd msg
savePresentation presentation =
    presentation
        |> toString
        |> storePresentation


presentationLoaded : (Maybe SearchResultPresentation -> msg) -> Sub msg
presentationLoaded toMsg =
    onPresentationChanged (readFromString >> toMsg)


toString presentation =
    case presentation of
        Textual BoundedContext.Full ->
            "Textual:Full"

        Textual BoundedContext.Condensed ->
            "Textual:Condensed"

        Sunburst Filtered ->
            "Sunburst:Filtered"

        Sunburst Highlighted ->
            "Sunburst:Highlighted"

        Hierarchical ->
            "Hierarchical"


readFromString : String -> Maybe SearchResultPresentation
readFromString s =
    case s |> String.toLower of
        "textual:condensed" ->
            Just (Textual BoundedContext.Condensed)

        "textual:full" ->
            Just (Textual BoundedContext.Full)

        "sunburst:filtered" ->
            Just (Sunburst Filtered)

        "sunburst:highlighted" ->
            Just (Sunburst Highlighted)

        "Hierarchical" ->
            Just (Hierarchical)

        _ ->
            Nothing


port storePresentation : String -> Cmd msg


port onPresentationChanged : (String -> msg) -> Sub msg
