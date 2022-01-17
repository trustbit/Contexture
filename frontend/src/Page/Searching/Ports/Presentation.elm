port module Page.Searching.Ports.Presentation exposing (SearchResultPresentation(..), VisualPresentation(..), SunburstPresentation(..), presentationLoaded, savePresentation)

import Components.BoundedContextsOfDomain as BoundedContext


type SunburstPresentation
    = Filtered
    | Highlighted


type VisualPresentation
    = Sunburst SunburstPresentation
    | Hierarchical


type SearchResultPresentation
    = Textual BoundedContext.Presentation
    | Visual VisualPresentation


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

        Visual (Sunburst Filtered) ->
            "Sunburst:Filtered"

        Visual (Sunburst Highlighted) ->
            "Sunburst:Highlighted"

        Visual Hierarchical ->
            "Hierarchical"


readFromString : String -> Maybe SearchResultPresentation
readFromString s =
    case s |> String.toLower of
        "textual:condensed" ->
            Just (Textual BoundedContext.Condensed)

        "textual:full" ->
            Just (Textual BoundedContext.Full)

        "sunburst:filtered" ->
            Just (Visual (Sunburst Filtered))

        "sunburst:highlighted" ->
            Just (Visual (Sunburst Highlighted))

        "Hierarchical" ->
            Just (Visual Hierarchical)

        _ ->
            Nothing


port storePresentation : String -> Cmd msg


port onPresentationChanged : (String -> msg) -> Sub msg
