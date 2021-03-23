module Page.Bcc.Edit.Namespaces exposing
    ( Model
    , Msg
    , init
    , update
    , view
    )

import Api
import Array exposing (Array)
import Bootstrap.Accordion as Accordion
import Bootstrap.Button as Button
import Bootstrap.ButtonGroup as ButtonGroup
import Bootstrap.Card as Card
import Bootstrap.Card.Block as Block
import Bootstrap.Form as Form
import Bootstrap.Form.Fieldset as Fieldset
import Bootstrap.Form.Input as Input
import Bootstrap.Form.InputGroup as InputGroup
import Bootstrap.Grid as Grid
import Bootstrap.Grid.Col as Col
import Bootstrap.Grid.Row as Row
import Bootstrap.ListGroup as ListGroup
import Bootstrap.Utilities.Display as Display
import Bootstrap.Utilities.Flex as Flex
import Bootstrap.Utilities.Spacing as Spacing
import BoundedContext.BoundedContextId exposing (BoundedContextId)
import Html exposing (Html, button, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick)
import Http
import Json.Decode as Decode exposing (Decoder)
import Json.Decode.Pipeline as JP
import Json.Encode as Encode
import Page.Bcc.Edit.BusinessDecision exposing (Msg(..))
import RemoteData exposing (RemoteData)
import Url


type alias Uuid =
    String


type alias NamespaceId =
    Uuid


type alias NamespaceTemplateId =
    Int


type alias LabelId =
    Uuid


type alias Label =
    { id : LabelId
    , name : String
    , value : String
    }


type alias Namespace =
    { id : NamespaceId
    , template : Maybe NamespaceTemplateId
    , name : String
    , labels : List Label
    }


type alias NewLabel =
    { name : String
    , value : String
    }


type alias CreateNamespace =
    { name : String
    , labels : Array NewLabel
    }


type alias Model =
    { namespaces : RemoteData.WebData (List Namespace)
    , accordionState : Accordion.State
    , newNamespace : Maybe CreateNamespace
    , configuration : Api.Configuration
    , boundedContextId : BoundedContextId
    }


init : Api.Configuration -> BoundedContextId -> ( Model, Cmd Msg )
init config contextId =
    ( { namespaces = RemoteData.Loading
      , accordionState = Accordion.initialState
      , newNamespace = Nothing
      , configuration = config
      , boundedContextId = contextId
      }
    , loadNamespaces config contextId
    )


initNewLabel =
    { name = "", value = "" }


initNewNamespace =
    { name = ""
    , labels = Array.empty
    }


type Msg
    = NamespacesLoaded (Api.ApiResponse (List Namespace))
    | AccordionMsg Accordion.State
    | StartAddingNamespace
    | ChangeNamespace String
    | AppendNewLabel
    | UpdateLabelName Int String
    | UpdateLabelValue Int String
    | RemoveLabel Int
    | AddNamespace CreateNamespace
    | NamespaceAdded (Api.ApiResponse (List Namespace))
    | CancelAddingNamespace


appendNewLabel namespace =
    { namespace | labels = namespace.labels |> Array.push initNewLabel }


updateLabel index updateLabelProperty namespace =
    let
        item =
            case namespace.labels |> Array.get index of
                Just element ->
                    updateLabelProperty element

                Nothing ->
                    updateLabelProperty initNewLabel
    in
    { namespace | labels = namespace.labels |> Array.set index item }


removeLabel : Int -> Array a -> Array a
removeLabel i a =
    let
        a1 =
            Array.slice 0 i a

        a2 =
            Array.slice (i + 1) (Array.length a) a
    in
    Array.append a1 a2


update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case msg of
        NamespacesLoaded namespaces ->
            ( { model | namespaces = RemoteData.fromResult namespaces }, Cmd.none )

        AccordionMsg state ->
            ( { model | accordionState = state }, Cmd.none )

        StartAddingNamespace ->
            ( { model | newNamespace = Just initNewNamespace }, Cmd.none )

        ChangeNamespace name ->
            ( { model | newNamespace = model.newNamespace |> Maybe.map (\namespace -> { namespace | name = name }) }, Cmd.none )

        AppendNewLabel ->
            ( { model | newNamespace = model.newNamespace |> Maybe.map appendNewLabel }, Cmd.none )

        UpdateLabelName index name ->
            ( { model | newNamespace = model.newNamespace |> Maybe.map (updateLabel index (\l -> { l | name = name })) }, Cmd.none )

        UpdateLabelValue index value ->
            ( { model | newNamespace = model.newNamespace |> Maybe.map (updateLabel index (\l -> { l | value = value })) }, Cmd.none )

        RemoveLabel index ->
            ( { model | newNamespace = model.newNamespace |> Maybe.map (\namespace -> { namespace | labels = namespace.labels |> removeLabel index }) }, Cmd.none )

        AddNamespace namespace ->
            ( model, addNamespace model.configuration model.boundedContextId namespace )

        NamespaceAdded namespaces ->
            ( { model
                | namespaces = RemoteData.fromResult namespaces
                , newNamespace = Nothing
              }
            , Cmd.none
            )

        CancelAddingNamespace ->
            ( { model | newNamespace = Nothing }, Cmd.none )


viewLabel model =
    Block.custom <|
        Form.row []
            [ Form.colLabel [] [ text model.name ]
            , Form.col []
                [ Input.text [ Input.value model.value ] ]
            , Form.col [ Col.bottomSm ]
                [ Button.button [ Button.secondary ] [ text "X" ] ]
            ]


viewNamespace model =
    Accordion.card
        { id = model.id
        , options = []
        , header = Accordion.header [] <| Accordion.toggle [] [ text model.name ]
        , blocks = model.labels |> List.map viewLabel |> List.map List.singleton |> List.map (Accordion.block [])
        }


view : Model -> Html Msg
view model =
    Card.config [ Card.attrs [ class "mb-3", class "shadow" ] ]
        |> Card.block []
            [ Block.titleH4 [] [ text "Namespaces" ]
            ]
        |> Card.block []
            (case model.namespaces of
                RemoteData.Success namespaces ->
                    Accordion.config AccordionMsg
                        |> Accordion.cards
                            (namespaces
                                |> List.map viewNamespace
                            )
                        |> Accordion.view model.accordionState
                        |> Block.custom
                        |> List.singleton

                e ->
                    [ e |> Debug.toString |> text |> Block.custom ]
            )
        |> Card.footer []
            [ case model.newNamespace of
                Nothing ->
                    Button.button
                        [ Button.primary
                        , Button.onClick StartAddingNamespace
                        ]
                        [ text "add a namespace" ]

                Just newNamespace ->
                    viewNewNamespace newNamespace
            ]
        |> Card.view


viewAddLabel index model =
    Form.row []
        [ Form.col []
            [ Form.label [] [ text "Label" ]
            , Input.text [ Input.placeholder "Label name", Input.value model.name, Input.onInput (UpdateLabelName index) ]
            ]
        , Form.col []
            [ Form.label [] [ text "Value" ]
            , Input.text [ Input.placeholder "Label value", Input.value model.value, Input.onInput (UpdateLabelValue index) ]
            ]
        , Form.col [ Col.bottomSm ]
            [ Button.button [ Button.secondary, Button.onClick (RemoveLabel index) ] [ text "X" ] ]
        ]


viewNewNamespace model =
    Form.form []
        (Form.row []
            [ Form.col []
                [ Form.label [ for "namespace" ] [ text "Namespace" ]
                , Input.text [ Input.id "namespace", Input.placeholder "The name of namespace containing the labels", Input.onInput ChangeNamespace ]
                ]
            ]
            :: (model.labels |> Array.indexedMap viewAddLabel |> Array.toList)
            ++ [ Form.row []
                    [ Form.col []
                        [ Button.button [ Button.secondary, Button.onClick AppendNewLabel ] [ text "New Label" ] ]
                    , Form.col [ Col.smAuto ]
                        [ ButtonGroup.buttonGroup []
                            [ ButtonGroup.button [ Button.secondary, Button.onClick CancelAddingNamespace ] [ text "Cancel" ]
                            , ButtonGroup.button [ Button.primary, Button.onClick (AddNamespace model) ] [ text "Add Namespace" ]
                            ]
                        ]
                    ]
               ]
        )


labelDecoder =
    Decode.map3 Label
        (Decode.field "id" Decode.string)
        (Decode.field "name" Decode.string)
        (Decode.field "value" Decode.string)


namespaceDecoder =
    Decode.map4 Namespace
        (Decode.field "id" Decode.string)
        (Decode.maybe (Decode.field "template" Decode.int))
        (Decode.field "name" Decode.string)
        (Decode.field "labels" (Decode.list labelDecoder))


labelEncoder model =
    Encode.object
        [ ( "name", Encode.string model.name )
        , ( "value", Encode.string model.value )
        ]


namespaceEncoder model =
    Encode.object
        [ ( "name", Encode.string model.name )
        , ( "labels", model.labels |> Array.toList |> Encode.list labelEncoder )
        ]


loadNamespaces : Api.Configuration -> BoundedContextId -> Cmd Msg
loadNamespaces config boundedContextId =
    Http.get
        { url = Api.boundedContext boundedContextId |> Api.url config |> Url.toString |> (\b -> b ++ "/namespaces")
        , expect = Http.expectJson NamespacesLoaded (Decode.list namespaceDecoder)
        }


addNamespace : Api.Configuration -> BoundedContextId -> CreateNamespace -> Cmd Msg
addNamespace config boundedContextId namespace =
    Http.post
        { url = Api.boundedContext boundedContextId |> Api.url config |> Url.toString |> (\b -> b ++ "/namespaces")
        , body = Http.jsonBody <| namespaceEncoder namespace
        , expect = Http.expectJson NamespaceAdded (Decode.list namespaceDecoder)
        }
