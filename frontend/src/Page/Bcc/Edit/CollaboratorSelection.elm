module Page.Bcc.Edit.CollaboratorSelection exposing (
  init, Model, CollaboratorReference(..),BoundedContextDependency,DomainDependency,CollaboratorReferenceType(..),
  update, Msg,
  view,
  collaboratorCaption)

import Html exposing (Html, button, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick)

import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input
import Bootstrap.Form.Textarea as Textarea
import Bootstrap.Form.Radio as Radio
import Bootstrap.Button as Button
import Bootstrap.ButtonGroup as ButtonGroup
import Bootstrap.Card.Block as Block
import Bootstrap.Card as Card
import Bootstrap.Modal as Modal
import Bootstrap.Form.Fieldset as Fieldset
import Bootstrap.Utilities.Spacing as Spacing
import Bootstrap.Utilities.Display as Display
import Bootstrap.Utilities.Border as Border
import Bootstrap.Utilities.Flex as Flex
import Bootstrap.ListGroup as ListGroup
import Bootstrap.Text as Text

import Select as Autocomplete

import BoundedContext.BoundedContextId as BoundedContext exposing(BoundedContextId)
import BoundedContext as BoundedContext
import Domain
import Domain.DomainId as Domain
import ContextMapping.Collaboration as ContextMapping exposing (..)
import ContextMapping.Collaborator as Collaborator exposing(Collaborator)


type alias DomainDependency =
  { id : Domain.DomainId
  , name : String }


type alias BoundedContextDependency =
  { id : BoundedContextId
  , name: String
  , domain: DomainDependency }

type CollaboratorReference
  = BoundedContext BoundedContextDependency
  | Domain DomainDependency
  | ExternalSystem String
  | Frontend String


type alias Model =
  { selectedCollaborator : Maybe CollaboratorReferenceType
  , availableDependencies : List CollaboratorReference
  , collaborator : Maybe Collaborator
  }


type CollaboratorReferenceType
  = BoundedContextType (Maybe BoundedContextDependency) Autocomplete.State
  | DomainType (Maybe DomainDependency) Autocomplete.State
  | ExternalSystemType (Maybe String)
  | FrontendType (Maybe String)


type Msg
  = SelectCollaborationType CollaboratorReferenceType
  | SelectBoundedContextMsg (Autocomplete.Msg BoundedContextDependency)
  | OnBoundedContextSelect (Maybe BoundedContextDependency)
  | SelectDomainMsg (Autocomplete.Msg DomainDependency)
  | OnDomainSelect (Maybe DomainDependency)
  | ExternalSystemCaption String
  | FrontendCaption String


init : List CollaboratorReference -> Model
init dependencies =
  { selectedCollaborator = Nothing
  , availableDependencies = dependencies
  , collaborator = Nothing
  }


updateSelectedCollaborator model collaboratorType =
  let
    collaborator =
      case collaboratorType of
        BoundedContextType (Just bc) _ ->
          Just <| Collaborator.BoundedContext bc.id
        DomainType (Just d) _ ->
          Just <| Collaborator.Domain d.id
        ExternalSystemType (Just e) ->
          Just <| Collaborator.ExternalSystem e
        FrontendType (Just f) ->
          Just <| Collaborator.Frontend f
        _ ->
          Nothing
  in
    { model
    | selectedCollaborator = Just <| collaboratorType
    , collaborator = collaborator
    }


update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case (msg, model.selectedCollaborator) of
    (SelectCollaborationType t, _) ->
      (updateSelectedCollaborator model t, Cmd.none)
    (SelectBoundedContextMsg selMsg, Just (BoundedContextType sel state)) ->
      let
        ( updated, cmd ) =
          Autocomplete.update selectBoundedContextConfig selMsg state
      in
        ( updateSelectedCollaborator model <| BoundedContextType sel updated , cmd )
    (OnBoundedContextSelect context, Just (BoundedContextType _ state)) ->
      ( updateSelectedCollaborator model <| BoundedContextType context state, Cmd.none)
    (SelectDomainMsg selMsg, Just (DomainType sel state)) ->
      let
        ( updated, cmd ) =
          Autocomplete.update selectDomainConfig selMsg state
      in
        ( updateSelectedCollaborator model <| DomainType sel updated, cmd )
    (OnDomainSelect domain, Just (DomainType _ state)) ->
      ( updateSelectedCollaborator model <| DomainType domain state, Cmd.none)
    (ExternalSystemCaption caption, Just (ExternalSystemType _)) ->
      ( updateSelectedCollaborator model <|
          ExternalSystemType
          ( if caption |> String.isEmpty
            then Nothing
            else Just caption
          )
      , Cmd.none
      )
    (FrontendCaption caption, Just (FrontendType _)) ->
      ( updateSelectedCollaborator model <|
          FrontendType
          ( if caption |> String.isEmpty
            then Nothing
            else Just caption
          )
      , Cmd.none
      )
    _ ->
      (model, Cmd.none)


filterLowerCase : Int -> (item -> List String) -> String -> List item -> Maybe (List item)
filterLowerCase minChars stringsToCompare query items =
  if String.length query < minChars then
      Nothing
  else
    let
      lowerQuery = query |> String.toLower
      containsLowerString text =
        text
        |> String.toLower
        |> String.contains lowerQuery
      searchable i =
        i
        |> stringsToCompare
        |> List.any containsLowerString
      in
        items
        |> List.filter searchable
        |> Just


selectBoundedContextConfig : Autocomplete.Config Msg BoundedContextDependency
selectBoundedContextConfig =
  Autocomplete.newConfig
      { onSelect = OnBoundedContextSelect
      , toLabel = (\bc -> bc.domain.name ++ " - " ++ bc.name)
      , filter = filterLowerCase 2 (\bc -> [ bc.name, bc.domain.name ])
      }
      |> Autocomplete.withCutoff 12
      |> Autocomplete.withInputClass "text-control border rounded form-control-lg"
      |> Autocomplete.withInputWrapperClass ""
      |> Autocomplete.withItemClass " border p-2 "
      |> Autocomplete.withMenuClass "bg-light"
      |> Autocomplete.withNotFound "No matches"
      |> Autocomplete.withNotFoundClass "text-danger"
      |> Autocomplete.withHighlightedItemClass "bg-white"
      |> Autocomplete.withPrompt "Search for a Bounded Context"
      |> Autocomplete.withItemHtml (\bc ->
          Html.span []
            [ Html.h6 [ class "text-muted" ] [ text bc.domain.name ]
            , Html.span [] [ text bc.name ]
            ]
      )


selectDomainConfig : Autocomplete.Config Msg DomainDependency
selectDomainConfig =
  Autocomplete.newConfig
      { onSelect = OnDomainSelect
      , toLabel = (\domain -> domain.name )
      , filter = filterLowerCase 2 (\domain -> [ domain.name ])
      }
      |> Autocomplete.withCutoff 12
      |> Autocomplete.withInputClass "text-control border rounded form-control-lg"
      |> Autocomplete.withInputWrapperClass ""
      |> Autocomplete.withItemClass " border p-2 "
      |> Autocomplete.withMenuClass "bg-light"
      |> Autocomplete.withNotFound "No matches"
      |> Autocomplete.withNotFoundClass "text-danger"
      |> Autocomplete.withHighlightedItemClass "bg-white"
      |> Autocomplete.withPrompt "Search for a Domain"
      |> Autocomplete.withItemHtml (\domain ->
        Html.span [] [ text domain.name ]
      )


view : Model -> Html Msg
view model =
  let
    -- selectedItem =
    --   case model.selectedCollaborator of
    --     Just (Just (BoundedContextType s)) -> [ s ]
    --     Nothing -> []

    -- TODO: this is probably very unefficient
    -- existingDependencies =
    --   model.existingCollaboration
    --   |> List.map Tuple.first

    -- relevantDependencies =
    --   dependencies
    --   |> List.filter (\d -> existingDependencies |> List.any (\existing -> (d |> toCollaborator)  == existing ) |> not )
    radioList =
      Radio.radioList "collaboratorSelection"
        [ Radio.create
          [ Radio.id "boundedContextOption"
          , Radio.onClick (SelectCollaborationType (BoundedContextType Nothing (Autocomplete.newState "boundedContext")))
          , Radio.checked (
              case model.selectedCollaborator of
                Just (BoundedContextType _ _) -> True
                _ -> False
            )
          ]
          "Bounded Context"
        , Radio.create
          [ Radio.id "domainOption"
          , Radio.onClick (SelectCollaborationType (DomainType Nothing (Autocomplete.newState "domain")))
          , Radio.checked (
              case model.selectedCollaborator of
                Just (DomainType _ _) -> True
                _ -> False
            )
          ]
          "Domain"
        , Radio.create
          [ Radio.id "externalSystemOption"
          , Radio.onClick (SelectCollaborationType (ExternalSystemType Nothing))
          , Radio.checked (
              case model.selectedCollaborator of
                Just (ExternalSystemType _) -> True
                _ -> False
            )
          ]
          "External System"
         , Radio.create
          [ Radio.id "frontendOption"
          , Radio.onClick (SelectCollaborationType (FrontendType Nothing))
          , Radio.checked (
              case model.selectedCollaborator of
                Just (FrontendType _) -> True
                _ -> False
            )
          ]
          "Frontend"
        ]

    asList maybe =
      case maybe of
        Just value -> [ value ]
        Nothing -> []

  in div []
    [ Html.div [] radioList
    , case model.selectedCollaborator of
        Just selectedType ->
          case selectedType of
            BoundedContextType selected state ->
                Autocomplete.view
                selectBoundedContextConfig
                state
                ( model.availableDependencies
                  |> List.filterMap (\item ->
                      case item of
                        BoundedContext bc ->
                          Just bc
                        _ ->
                          Nothing
                    )
                )
                (asList selected)
              |> Html.map SelectBoundedContextMsg
            DomainType selected state ->
              Autocomplete.view
                selectDomainConfig
                state
                ( model.availableDependencies
                  |> List.filterMap (\item ->
                      case item of
                        Domain bc ->
                          Just bc
                        _ ->
                          Nothing
                    )
                )
                (asList selected)
              |> Html.map SelectDomainMsg
            ExternalSystemType value ->
              Input.text
                [ Input.value (value |> Maybe.withDefault "")
                , Input.placeholder "External System name"
                , Input.onInput ExternalSystemCaption
                ]
            FrontendType value ->
              Input.text
                [ Input.value (value |> Maybe.withDefault "")
                , Input.placeholder "Frontend name"
                , Input.onInput FrontendCaption
                ]
        Nothing ->
          Input.text
            [ Input.disabled True
            , Input.placeholder "Select an option"]
    ]


-- helpers

type alias LabelAndDescription = (String, String)

collaboratorCaption : List CollaboratorReference -> Collaborator -> LabelAndDescription
collaboratorCaption items collaborator=
  case collaborator of
    Collaborator.BoundedContext bc ->
      items
      |> List.filterMap (\r ->
        case r of
          BoundedContext bcr ->
            if bcr.id == bc then
              Just <|
                ( bcr.name
                , "[in Domain '" ++ bcr.domain.name  ++ "']"
                )
            else
              Nothing
          _ ->
            Nothing
      )
      |> List.head
      |> Maybe.withDefault ("Unknown Bounded Context", "")
    Collaborator.Domain d ->
      items
      |> List.filterMap (\r ->
        case r of
          Domain dr ->
            if dr.id == d
            then Just (dr.name, "[Domain]")
            else Nothing
          _ ->
            Nothing
      )
      |> List.head
      |> Maybe.withDefault ("Unknown Domain", "")
    Collaborator.ExternalSystem s ->
      ( s
      , "[External System]"
      )
    Collaborator.Frontend s ->
      ( s
      , "[Frontend]"
      )
    Collaborator.UserInteraction s ->
      ( s
      , "[User Interaction]"
      )
