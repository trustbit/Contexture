port module Page.Searching.Ports exposing (SearchResultPresentation(..), store, read)

import Components.BoundedContextsOfDomain as BoundedContext
    
type SearchResultPresentation
    = Textual BoundedContext.Presentation
    | Sunburst
    
asText presentation =
    case presentation of
        Textual BoundedContext.Full ->
            "Textual:Full"

        Textual BoundedContext.Condensed ->
            "Textual:Condensed"
        
        Sunburst ->
            "Sunburst"

store : SearchResultPresentation -> Cmd msg 
store presentation =
    presentation
    |> asText
    |> storePresentation
    
read : String -> Maybe SearchResultPresentation
read s =
    case s |> String.toLower of
        "textual:condensed" ->
            Just (Textual BoundedContext.Condensed)
        "textual:full" ->
            Just (Textual BoundedContext.Full)
        "sunburst" ->
            Just Sunburst
        _ ->
            Nothing
                            
    
port storePresentation : String -> Cmd msg
