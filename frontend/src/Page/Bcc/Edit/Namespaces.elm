module Page.Bcc.Edit.Namespaces exposing
    ( Model
    , Msg
    , init
    , update
    , view
    , subscriptions
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
import Bootstrap.Text as Text
import Bootstrap.Utilities.Display as Display
import Bootstrap.Utilities.Flex as Flex
import Bootstrap.Utilities.Spacing as Spacing
import Bootstrap.Dropdown as Dropdown
import BoundedContext.BoundedContextId exposing (BoundedContextId)
import BoundedContext.Namespace exposing (..)
import Html exposing (Html, button, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick, onSubmit)
import Http
import Json.Decode as Decode exposing (Decoder)
import Json.Decode.Pipeline as JP
import Json.Encode as Encode
import RemoteData exposing (RemoteData)
import Set
import Url


type alias NewLabel =
    { name : String
    , value : String
    , isValid : Bool
    , template : Maybe LabelTemplate
    }

type NamespaceNameError
    = NoName
    | NameIsNotUnique


type alias CreateNamespace =
    { name : String
    , value : Result NamespaceNameError String
    , labels : Array NewLabel
    , template : Maybe NamespaceTemplate
    }


type alias NamespaceModel =
    { namespace : Namespace
    , addLabel : Maybe NewLabel
    }

type alias TemplateModel =
    { state : Dropdown.State
    , namespaces : List NamespaceTemplate
    }


type alias Model =
    { namespaces : RemoteData.WebData (List NamespaceModel)
    , templates : RemoteData.WebData TemplateModel
    , accordionState : Accordion.State
    , newNamespace : Maybe CreateNamespace
    , configuration : Api.Configuration
    , boundedContextId : BoundedContextId
    }


initNamespace : Api.ApiResponse (List Namespace) -> RemoteData.WebData (List NamespaceModel)
initNamespace namespaceResult =
    namespaceResult
        |> RemoteData.fromResult
        |> RemoteData.map
            (\namespaces ->
                namespaces
                    |> List.map
                        (\n ->
                            { namespace = n, addLabel = Nothing }
                        )
            )


init : Api.Configuration -> BoundedContextId -> ( Model, Cmd Msg )
init config contextId =
    ( { namespaces = RemoteData.Loading
      , templates = RemoteData.Loading
      , accordionState = Accordion.initialState
      , newNamespace = Nothing
      , configuration = config
      , boundedContextId = contextId
      }
    , Cmd.batch [ loadNamespaces config contextId, loadTemplates config ]
    )


initNewLabel =
    { name = "", value = "", isValid = False, template = Nothing }

initLabelFromTemplate : LabelTemplate -> NewLabel
initLabelFromTemplate template =
    { name = template.name, value = "", isValid = True, template = Just template}


initNewNamespace : CreateNamespace
initNewNamespace =
    { name = ""
    , value = Err NoName
    , labels = Array.empty
    , template = Nothing
    }

initNewNamespaceFromTemplate : (String -> Result NamespaceNameError String) -> NamespaceTemplate -> CreateNamespace
initNewNamespaceFromTemplate validateName template =
    { name = template.name
    , value = validateName template.name
    , labels =
        template.template
        |> List.map initLabelFromTemplate
        |> Array.fromList
    , template = Just template
    }

type Msg
    = NamespacesLoaded (Api.ApiResponse (List Namespace))
    | NamespacesTemplatesLoaded (Api.ApiResponse (List NamespaceTemplate))
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
    | RemoveNamespace NamespaceId
    | NamespaceRemoved (Api.ApiResponse (List Namespace))
    | RemoveLabelFromNamespace NamespaceId LabelId
    | LabelRemoved (Api.ApiResponse (List Namespace))
    | AddingLabelToExistingNamespace NamespaceId
    | UpdateLabelNameForExistingNamespace NamespaceId String
    | UpdateLabelValueForExistingNamespace NamespaceId String
    | AddLabelToExistingNamespace NamespaceId NewLabel
    | CancelAddingLabelToExistingNamespace NamespaceId
    | LabelAddedToNamespace (Api.ApiResponse (List Namespace))
    | DropdownMsg Dropdown.State
    | StartAddingNamespaceFromTemplate NamespaceTemplate


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


editingNamespace namespaceId updateNamespace namespaces =
    namespaces
        |> List.map
            (\n ->
                if n.namespace.id == namespaceId then
                    updateNamespace n

                else
                    n
            )


removeLabel : Int -> Array a -> Array a
removeLabel i a =
    let
        a1 =
            Array.slice 0 i a

        a2 =
            Array.slice (i + 1) (Array.length a) a
    in
    Array.append a1 a2


updateLabelName name label =
    { label
        | name = name
        , isValid = not <| String.isEmpty name
    }


namespaceNameShouldNotBeEmpty value =
    String.isEmpty value

namespaceNameShouldBeUnique namespaces value =
    namespaces
    |> RemoteData.withDefault []
    |> List.map (\n -> n.namespace.name)
    |> Set.fromList
    |> Set.map String.toLower
    |> Set.member (value |> String.toLower)


parseNamespaceName namespaces value =
    let
        trimmed = String.trim value
    in
        if namespaceNameShouldNotBeEmpty trimmed then
            Err NoName
        else if namespaceNameShouldBeUnique namespaces trimmed then
            Err NameIsNotUnique
        else
            Ok trimmed


updateNamespaceName value parse namespace =
    { namespace | name = value, value = parse value }


update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case msg of
        NamespacesLoaded namespaces ->
            ( { model | namespaces = initNamespace namespaces }, Cmd.none )

        NamespacesTemplatesLoaded templates ->
            ( { model | templates = templates |> RemoteData.fromResult |> RemoteData.map (\t -> { state = Dropdown.initialState, namespaces = t})  }, Cmd.none )

        AccordionMsg state ->
            ( { model | accordionState = state }, Cmd.none )

        StartAddingNamespace ->
            ( { model | newNamespace = initNewNamespace |> Just }, Cmd.none )

        ChangeNamespace name ->
            ( { model
                | newNamespace =
                    model.newNamespace
                        |> Maybe.map (updateNamespaceName name (parseNamespaceName model.namespaces))
              }
            , Cmd.none
            )

        AppendNewLabel ->
            ( { model | newNamespace = model.newNamespace |> Maybe.map appendNewLabel }, Cmd.none )

        UpdateLabelName index name ->
            ( { model | newNamespace = model.newNamespace |> Maybe.map (updateLabel index (updateLabelName name)) }, Cmd.none )

        UpdateLabelValue index value ->
            ( { model | newNamespace = model.newNamespace |> Maybe.map (updateLabel index (\l -> { l | value = value })) }, Cmd.none )

        RemoveLabel index ->
            ( { model | newNamespace = model.newNamespace |> Maybe.map (\namespace -> { namespace | labels = namespace.labels |> removeLabel index }) }, Cmd.none )

        AddNamespace namespace ->
            ( model, addNamespace model.configuration model.boundedContextId namespace )

        NamespaceAdded namespaces ->
            ( { model
                | namespaces = initNamespace namespaces
                , newNamespace = Nothing
              }
            , Cmd.none
            )

        CancelAddingNamespace ->
            ( { model | newNamespace = Nothing }, Cmd.none )

        RemoveNamespace namespaceId ->
            ( model, removeNamespace model.configuration model.boundedContextId namespaceId )

        NamespaceRemoved namespaces ->
            ( { model | namespaces = initNamespace namespaces }, Cmd.none )

        RemoveLabelFromNamespace namespace label ->
            ( model, removeLabelFromNamespace model.configuration model.boundedContextId namespace label )

        LabelRemoved namespaces ->
            ( { model | namespaces = initNamespace namespaces }, Cmd.none )

        AddingLabelToExistingNamespace namespace ->
            ( { model | namespaces = model.namespaces |> RemoteData.map (editingNamespace namespace (\n -> { n | addLabel = Just initNewLabel })) }, Cmd.none )

        UpdateLabelNameForExistingNamespace namespace name ->
            ( { model | namespaces = model.namespaces |> RemoteData.map (editingNamespace namespace (\n -> { n | addLabel = n.addLabel |> Maybe.map (updateLabelName name) })) }, Cmd.none )

        UpdateLabelValueForExistingNamespace namespace value ->
            ( { model | namespaces = model.namespaces |> RemoteData.map (editingNamespace namespace (\n -> { n | addLabel = n.addLabel |> Maybe.map (\l -> { l | value = value }) })) }, Cmd.none )

        CancelAddingLabelToExistingNamespace namespace ->
            ( { model | namespaces = model.namespaces |> RemoteData.map (editingNamespace namespace (\n -> { n | addLabel = Nothing })) }, Cmd.none )

        AddLabelToExistingNamespace namespace newLabel ->
            ( model, addLabelToNamespace model.configuration model.boundedContextId namespace newLabel )

        LabelAddedToNamespace namespaces ->
            ( { model | namespaces = initNamespace namespaces }, Cmd.none )

        DropdownMsg state ->
            ( { model | templates = model.templates |> RemoteData.map (\t -> { t | state = state})}, Cmd.none)

        StartAddingNamespaceFromTemplate template ->
            ( { model | newNamespace = template |> initNewNamespaceFromTemplate (parseNamespaceName model.namespaces) |> Just }, Cmd.none)

viewLabelAsLink value =
    case Url.fromString value of
        Just link ->
           Button.linkButton [ Button.attrs [ link |> Url.toString |> href, target "_blank"], Button.small ] [ 0x0001F517 |>Char.fromCode  |>  String.fromChar |> Html.text]
        Nothing ->
            text ""


viewAddLabelToExistingNamespace namespace model =
    Form.form [ onSubmit (AddLabelToExistingNamespace namespace model) ]
        [ Form.row []
            [ Form.col []
                [ Form.label [] [ text "Label" ]
                , Input.text
                    [ Input.placeholder "Label name"
                    , Input.value model.name
                    , if model.isValid then
                        Input.success

                      else
                        Input.danger
                    , Input.onInput (UpdateLabelNameForExistingNamespace namespace)
                    ]
                ]
            , Form.col []
                [ Form.label [] [ text "Value" ]
                , Input.text [ Input.placeholder "Label value", Input.value model.value, Input.onInput (UpdateLabelValueForExistingNamespace namespace) ]
                ]
            , Form.col [ Col.sm1, Col.bottomSm  ] [ viewLabelAsLink model.value ]
            , Form.col [ Col.bottomSm ]
                [ ButtonGroup.buttonGroup []
                    [ ButtonGroup.button [ Button.secondary, Button.onClick (CancelAddingLabelToExistingNamespace namespace), Button.attrs [ type_ "button" ] ] [ text "Cancel" ]
                    , ButtonGroup.button
                        [ Button.primary, Button.disabled (not <| model.isValid) ]
                        [ text "Add Label" ]
                    ]
                ]
            ]
        ]


viewLabel namespace model =
    Block.custom <|
        Form.row []
            [ Form.colLabel [] [ text model.name ]
            , Form.col []
                [ Input.text [ Input.disabled True, Input.value model.value ]
                ]
            , Form.col [ Col.sm1 ] [ viewLabelAsLink model.value ]
            , Form.col [ Col.bottomSm ]
                [ Button.button [ Button.secondary, Button.onClick (RemoveLabelFromNamespace namespace model.id) ] [ text "X" ] ]
            ]


viewTemplateButton model =
    case model of
        RemoteData.Success { state, namespaces} ->
            Dropdown.dropdown
                state
                { options = [ ]
                , toggleMsg = DropdownMsg
                , toggleButton =
                    Dropdown.toggle [ Button.secondary ] [ text "Add Namespace from Template" ]
                , items =
                    namespaces
                    |> List.map (\n ->  Dropdown.buttonItem [ onClick (StartAddingNamespaceFromTemplate n), n.description |> Maybe.withDefault "Add from Template" |> title  ] [ text n.name ])
                }
        _ -> text ""


viewNamespace : NamespaceModel -> Accordion.Card Msg
viewNamespace { namespace, addLabel } =
    Accordion.card
        { id = namespace.id
        , options = []
        , header = Accordion.header [] <| Accordion.toggle [] [ text namespace.name ]
        , blocks =
            [ Accordion.block []
                (namespace.labels |> List.map (viewLabel namespace.id))
            , Accordion.block [] 
                ( case addLabel of
                    Just label ->
                        [ Block.custom <| viewAddLabelToExistingNamespace namespace.id label ]
                    Nothing ->
                        []
                )
            , Accordion.block []
                    [ Block.custom <|
                        Grid.row []
                            [ Grid.col []
                                [ Button.button
                                    [ Button.primary, Button.onClick (AddingLabelToExistingNamespace namespace.id) ]
                                    [ text "Add Label" ]
                                ]
                            , Grid.col [ Col.textAlign Text.alignSmRight ]
                                [ Button.button
                                    [ Button.secondary, Button.onClick (RemoveNamespace namespace.id), Button.attrs [ class "align-sm-right" ] ]
                                    [ text "Remove Namespace" ]
                                ]
                            ]
                    ]
                ]            
        }


view : Model -> Html Msg
view model =
    Card.config [ Card.attrs [ class "mb-3", class "shadow" ] ]
        |> Card.block []
            [ Block.titleH4 [] [ text "Namespaces" ]
            , Block.text [] [ text "Add semi-structured information about the bounded context."]
            ]
        |> Card.block []
            (case model.namespaces of
                RemoteData.Success namespaces ->
                    Accordion.config AccordionMsg
                        |> Accordion.withAnimation
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
                    div [ class "btn-group"]
                        [ Button.button
                            [ Button.primary
                            , Button.onClick StartAddingNamespace
                            ]
                            [ text "Add a new Namespace" ]
                        , viewTemplateButton model.templates
                        ]
                Just newNamespace ->
                    viewNewNamespace newNamespace
            ]
        |> Card.view


viewAddLabel index model =
    case model.template of
        Nothing ->
            Form.row []
                [ Form.col []
                    [ Form.label [] [ text "Label" ]
                    , Input.text
                        [ Input.placeholder "Label name"
                        , Input.value model.name
                        , if model.isValid then
                            Input.success

                        else
                            Input.danger
                        , Input.onInput (UpdateLabelName index)
                        ]
                    ]
                , Form.col []
                    [ Form.label [] [ text "Value" ]
                    , Input.text [ Input.placeholder "Label value", Input.value model.value, Input.onInput (UpdateLabelValue index) ]
                    ]
                , Form.col [ Col.sm1, Col.bottomSm  ] [ viewLabelAsLink model.value ]
                , Form.col [ Col.bottomSm ]
                    [ Button.button [ Button.roleLink, Button.onClick (RemoveLabel index), Button.attrs [ type_ "button" ] ] [ text "X" ] ]
                ]
        Just template ->
            Form.row []
                [ Form.col []
                    [ Form.label [] [ text template.name ]
                    , case template.description of
                        Just description ->
                            Form.help [] [ text description]
                        Nothing ->
                            text ""
                    ]
                , Form.col []
                    [ Input.text
                        [ template.placeholder |> Maybe.withDefault ("Value of " ++ template.name) |> Input.placeholder
                        , Input.value model.value
                        , Input.onInput (UpdateLabelValue index)
                        ]
                    ]
                , Form.col [ Col.sm1, Col.topSm  ] [ viewLabelAsLink model.value ]
                , Form.col [ Col.topSm ]
                    [ Button.button [ Button.roleLink, Button.onClick (RemoveLabel index), Button.attrs [ type_ "button" ] ] [ text "X" ] ]
                ]


viewNewNamespace model =
    let
        (isValid, description) =
            case model.value of
                Ok _ ->
                    (True, "")
                Err NoName ->
                    (False, "Please enter a namespace name")
                Err NameIsNotUnique ->
                    (False, "The namespace name is not unique")
    in Form.form [ onSubmit (AddNamespace model) ]
        (Form.row []
            [ Form.col []
                [ Form.label [ for "namespace" ] [ text "Namespace" ]
                , Input.text
                    [ Input.id "namespace"
                    , Input.placeholder "The name of namespace containing the labels"
                    , Input.value model.name
                    , Input.onInput ChangeNamespace
                    , if isValid then
                        Input.success
                      else
                        Input.danger
                    ]
                , Form.invalidFeedback [] [ text description]
                ]
            ]
            :: (model.labels |> Array.indexedMap viewAddLabel |> Array.toList)
            ++ [ Form.row []
                    [ Form.col []
                        [ Button.button [ Button.secondary, Button.onClick AppendNewLabel ] [ text "New Label" ] ]
                    , Form.col [ Col.smAuto ]
                        [ ButtonGroup.buttonGroup []
                            [ ButtonGroup.button [ Button.secondary, Button.onClick CancelAddingNamespace ] [ text "Cancel" ]
                            , ButtonGroup.button
                                [ Button.primary, Button.disabled <| not isValid, Button.attrs [ type_ "submit" ] ]
                                [ model.template |> Maybe.map (\t -> "Save namespace based on " ++ t.name) |> Maybe.withDefault "Save new Namespace" |> text ]
                            ]
                        ]
                    ]
               ]
        )


labelEncoder : NewLabel -> Encode.Value
labelEncoder model =
    Encode.object
        [ ( "name", Encode.string model.name )
        , ( "value", Encode.string model.value )
        , ( "template", model.template |> Maybe.map (\t -> Encode.string t.id) |> Maybe.withDefault Encode.null )
        ]


namespaceEncoder : CreateNamespace -> Encode.Value
namespaceEncoder model =
    Encode.object
        [ ( "name", Encode.string model.name )
        , ( "labels", model.labels |> Array.toList |> Encode.list labelEncoder )
        , ( "template", model.template |> Maybe.map (\t -> Encode.string t.id) |> Maybe.withDefault Encode.null )
        ]


loadNamespaces : Api.Configuration -> BoundedContextId -> Cmd Msg
loadNamespaces config boundedContextId =
    Http.get
        { url = Api.boundedContext boundedContextId |> Api.url config  |> (\b -> b ++ "/namespaces")
        , expect = Http.expectJson NamespacesLoaded (Decode.list namespaceDecoder)
        }


loadTemplates : Api.Configuration -> Cmd Msg
loadTemplates config =
    Http.get
        { url = Api.namespaceTemplates |> Api.url config
        , expect = Http.expectJson NamespacesTemplatesLoaded (Decode.list namespaceTemplateDecoder)
        }


addNamespace : Api.Configuration -> BoundedContextId -> CreateNamespace -> Cmd Msg
addNamespace config boundedContextId namespace =
    Http.post
        { url = Api.boundedContext boundedContextId |> Api.url config  |> (\b -> b ++ "/namespaces")
        , body = Http.jsonBody <| namespaceEncoder namespace
        , expect = Http.expectJson NamespaceAdded (Decode.list namespaceDecoder)
        }


removeNamespace : Api.Configuration -> BoundedContextId -> NamespaceId -> Cmd Msg
removeNamespace config boundedContextId namespace =
    Http.request
        { method = "DELETE"
        , url = Api.boundedContext boundedContextId |> Api.url config  |> (\b -> b ++ "/namespaces/" ++ namespace)
        , body = Http.emptyBody
        , expect = Http.expectJson NamespaceRemoved (Decode.list namespaceDecoder)
        , timeout = Nothing
        , tracker = Nothing
        , headers = []
        }


removeLabelFromNamespace : Api.Configuration -> BoundedContextId -> NamespaceId -> LabelId -> Cmd Msg
removeLabelFromNamespace config boundedContextId namespace label =
    Http.request
        { method = "DELETE"
        , url = Api.boundedContext boundedContextId |> Api.url config  |> (\b -> b ++ "/namespaces/" ++ namespace ++ "/labels/" ++ label)
        , body = Http.emptyBody
        , expect = Http.expectJson LabelRemoved (Decode.list namespaceDecoder)
        , timeout = Nothing
        , tracker = Nothing
        , headers = []
        }


addLabelToNamespace : Api.Configuration -> BoundedContextId -> NamespaceId -> NewLabel -> Cmd Msg
addLabelToNamespace config boundedContextId namespace label =
    Http.post
        { url = Api.boundedContext boundedContextId |> Api.url config  |> (\b -> b ++ "/namespaces/" ++ namespace ++ "/labels")
        , body = Http.jsonBody <| labelEncoder label
        , expect = Http.expectJson LabelAddedToNamespace (Decode.list namespaceDecoder)
        }


subscriptions : Model -> Sub Msg
subscriptions model =
    List.append
        [ Accordion.subscriptions model.accordionState AccordionMsg ]
        ( case model.templates of
            RemoteData.Success t ->
                [ Dropdown.subscriptions t.state DropdownMsg ]
            _ ->
                []
        )
    |> Sub.batch
