port module Page.Searching.Ports exposing (SearchResultPresentation(..),SunburstPresentation(..), store, read)

import Components.BoundedContextsOfDomain as BoundedContext
    
type SunburstPresentation
    = Filtered
    | Highlighted
type SearchResultPresentation
    = Textual BoundedContext.Presentation
    | Sunburst SunburstPresentation
    
asText presentation =
    case presentation of
        Textual BoundedContext.Full ->
            "Textual:Full"

        Textual BoundedContext.Condensed ->
            "Textual:Condensed"
        
        Sunburst Filtered ->
            "Sunburst:Filtered"
        
        Sunburst Highlighted ->
            "Sunburst:Highlighted"
            

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
        "sunburst:filtered" ->
            Just (Sunburst Filtered)
        "sunburst:highlighted" ->
                    Just (Sunburst Highlighted) 
        _ ->
            Nothing
                            
    
port storePresentation : String -> Cmd msg
