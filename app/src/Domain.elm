module Domain exposing (
  Domain, Model, init,
  Msg(..),update,ifNameValid,
  domainDecoder, domainsDecoder, modelEncoder, idFieldDecoder, nameFieldDecoder
  )

import Json.Decode as Decode exposing(Decoder)
import Json.Decode.Pipeline as JP
import Json.Encode as Encode

import Domain.DomainId exposing(DomainId(..), idDecoder)

-- MODEL

type alias Domain =
  { id : DomainId
  , name: String
  , vision: String
  , parentDomain: Maybe DomainId
  }

type alias Model = Domain

init : () -> Domain
init _ =
    { id = DomainId(-1)
    , name = ""
    , vision = "" 
    , parentDomain = Nothing}

-- UPDATE

type Msg
  = SetName String
  | SetVision String

update : Msg -> Model -> Model
update msg model =
  case msg of
    SetName name ->
      { model | name = name}
    SetVision vision->
      { model | vision = vision}

-- VIEW

ifValid : (model -> Bool) -> (model -> result) -> (model -> result) -> model -> result
ifValid predicate trueRenderer falseRenderer model =
  if predicate model then
    trueRenderer model
  else
    falseRenderer model

ifNameValid =
  ifValid (\name -> String.length name <= 0)


-- CONVERSIONS

nameFieldDecoder : Decoder String
nameFieldDecoder =
  Decode.field "name" Decode.string

idFieldDecoder : Decoder DomainId
idFieldDecoder =
  Decode.field "id" idDecoder

domainDecoder: Decoder Domain
domainDecoder =
  Decode.succeed Domain
    |> JP.custom idFieldDecoder
    |> JP.custom nameFieldDecoder
    |> JP.optional "vision" Decode.string ""
    |> JP.optional "domainId" (Decode.maybe idDecoder) Nothing

domainsDecoder: Decode.Decoder (List Domain)
domainsDecoder =
  Decode.list domainDecoder

modelEncoder : Domain -> Encode.Value
modelEncoder model =
    Encode.object
        [ ("name", Encode.string model.name)
        , ("vision", Encode.string model.vision)
        ]

