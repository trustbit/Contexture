module Page.Bcc.Edit.DomainRoles exposing (..)

import BoundedContext.DomainRoles as DomainRoles exposing (..)

import Html exposing (Html, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick, onSubmit)

import Bootstrap.Button as Button
import Bootstrap.ButtonGroup as ButtonGroup
import Bootstrap.Card as Card
import Bootstrap.Card.Block as Block
import Bootstrap.Text as Text
import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input
import Bootstrap.Form.Textarea as Textarea
import Bootstrap.ListGroup as ListGroup
import BoundedContext.BoundedContextId exposing (BoundedContextId)
import Api

type Msg
    = ShowCreateNew
    | ShowChooseFrom
    | CreateNew DomainRole
    | ChangeName String
    | ChangeDescription String
    | CancelCreating
    | Delete String
    | SelectRole String String
    | RolesLoaded (Api.ApiResponse DomainRoles)
    | RolesAdded (Api.ApiResponse DomainRoles)
    | RoleRemoved (Api.ApiResponse DomainRoles)

type ChangingModel
  = AddingNewDomainRole String String (Result DomainRoles.Problem DomainRole)
  | SelectingDomainRole String String (Result DomainRoles.Problem DomainRole)

type alias Model =
  { roles : DomainRoles
  , changingModel : Maybe ChangingModel
  , config : Api.Configuration
  , boundedContextId : BoundedContextId
  }


init : Api.Configuration -> BoundedContextId -> (Model, Cmd Msg)
init configuration contextId =
  ( { roles = []
    , changingModel = Nothing
    , config = configuration
    , boundedContextId = contextId
    }
  , getDomainRoles configuration contextId RolesLoaded)


noCommand model = (model, Cmd.none)

update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
    case (msg, model.changingModel) of
        (ShowCreateNew, _) ->
            noCommand { model | changingModel = Just <| AddingNewDomainRole "" "" (createDomainRole model.roles "" "") }

        (ChangeName name, Just (AddingNewDomainRole _ description _)) ->
            noCommand { model | changingModel = Just <| AddingNewDomainRole name description (createDomainRole model.roles name description) }

        (ChangeDescription description, Just (AddingNewDomainRole name _ _)) ->
            noCommand { model | changingModel = Just <| AddingNewDomainRole name description (createDomainRole model.roles name description) }

        (SelectRole name description, Just (SelectingDomainRole _ _ _)) ->
            noCommand { model | changingModel = Just <| SelectingDomainRole name description (createDomainRole model.roles name description)}

        (CancelCreating, _) ->
            noCommand { model | changingModel = Nothing}

        (CreateNew role, Just (AddingNewDomainRole _ _ _)) ->
            let
                newRole = addDomainRole model.config model.boundedContextId model.roles role

            in
                case newRole of
                    Ok roles ->
                        ({ model | changingModel = Nothing}, roles RolesAdded)
                    Err _ ->
                        noCommand  model

        (CreateNew role, Just (SelectingDomainRole _ _ _)) ->
            let
                newRole = addDomainRole model.config model.boundedContextId model.roles role

            in
                case newRole of
                    Ok roles ->
                        ({ model | changingModel = Nothing}, roles RolesAdded)
                    Err _ ->
                        noCommand model

        (Delete name, Nothing) ->
            (model, deleteDomainRole model.config model.boundedContextId model.roles name RoleRemoved)

        (ShowChooseFrom, _) ->
            noCommand { model | changingModel = Just <| SelectingDomainRole "" "" (createDomainRole model.roles "" "") }

        (RolesAdded (Ok roles), _) ->
            noCommand { model | roles = roles }
        (RoleRemoved (Ok roles), _) ->
           noCommand { model | roles = roles }
        (RolesLoaded (Ok roles), _) ->
           noCommand { model | roles = roles }

        _ ->
            noCommand model

view : Model -> Html Msg
view model =
    Html.div []
    [   viewCreateRole model.roles model.changingModel |> Card.view
    ,   Html.dl []
        (
            List.map viewRole model.roles |> List.concat
        )
    ]


viewRole : DomainRole -> List (Html Msg)
viewRole role =
  [ Html.dt []
    [ text (getName role)
    , Button.button
      [ Button.secondary
      , Button.small
      , Button.onClick (Delete (role |> getId))
      , Button.attrs [class "float-right"]
      ]
      [ text "X" ]
    ]
  , Html.dd
    []
    [ getDescription role
      |> Maybe.map text
      |> Maybe.withDefault (Html.i [] [ text "No description :-(" ])
    ]
  ]


viewCreateRole : List DomainRole -> Maybe ChangingModel -> Card.Config Msg
viewCreateRole domainRoles model =
    case model of
        Just (SelectingDomainRole name description result) ->
            let
                (nameIsValid, anEvent, feedbackText) =
                    case result of
                        Ok d ->
                            (True, [ onSubmit (CreateNew d) ], "")
                        Err p ->
                            let
                                errorText =
                                    case p of
                                        DefinitionEmpty -> "No domain role name is specified"
                                        AlreadyExists -> "The domain role with name '" ++ name ++ "' has already been defined before. Please use a distinct, case insensitive name."
                            in
                            (False, [], errorText)
            in
                Card.config [ Card.attrs [ class "mb-3", class "shadow" ] ]
                |> Card.block []
                [ Block.custom <|
                    Form.form anEvent [
                        Form.group []
                        [ Form.label [ for "role_select" ] [ text "Select role from the list:" ]
                        , viewSelectDomainRole domainRoles name
                        , Button.button [ Button.outlineSecondary, Button.onClick CancelCreating, Button.attrs [ class "mr-2"] ] [ text "Cancel"]
                        , Button.submitButton [ Button.primary, Button.disabled (not nameIsValid) ] [ text "Add this domain role" ]
                        ]
                    ]
                ]

        Just (AddingNewDomainRole name description result) ->
            let
                (nameIsValid, anEvent, feedbackText) =
                    case result of
                        Ok d ->
                            (True, [ onSubmit (CreateNew d) ], "")
                        Err p ->
                            let
                                errorText =
                                    case p of
                                        DefinitionEmpty -> "No domain role name is specified"
                                        AlreadyExists -> "The domain role with name '" ++ name ++ "' has already been defined before. Please use a distinct, case insensitive name."
                            in
                            (False, [], errorText)
            in
                Card.config [ Card.attrs [ class "mb-3", class "shadow" ] ]
                |> Card.block []
                [ Block.custom <|
                    Form.form anEvent
                    [ Form.group []
                    [ Form.label [ for "name" ] [ text "Domain role name" ]
                    , Input.text
                        [ Input.id "name"
                        , Input.value name
                        , Input.onInput ChangeName
                        , if nameIsValid
                            then Input.success
                            else Input.danger
                        ]
                    , Form.help [] [ text "The domain role name that is used inside this bounded context." ]
                    , Form.invalidFeedback [] [ text feedbackText]
                    ]
                    , Form.group []
                    [ Form.label [ for "description" ] [ text "Description" ]
                    , Textarea.textarea
                        [ Textarea.id "description"
                        , Textarea.value description
                        , Textarea.onInput ChangeDescription
                        ]
                    , Form.help [] [ text "Define the meaning of the this domain role inside this bounded context." ]
                    ]
                    , Button.button [ Button.outlineSecondary, Button.onClick CancelCreating, Button.attrs [ class "mr-2"] ] [ text "Cancel"]
                    , Button.submitButton [ Button.primary, Button.disabled (not nameIsValid) ] [ text "Add new domain role" ]
                    ]
                ]
        _ ->
            Card.config [ Card.attrs [ class "mb-3", class "shadow" ], Card.align Text.alignXsCenter ]
                |> Card.block []
            [ Block.custom <|
                ButtonGroup.buttonGroup []
                    [ ButtonGroup.button [ Button.primary, Button.onClick ShowChooseFrom ] [ text "Choose Role from pre-defined list" ]
                    , ButtonGroup.button [ Button.secondary, Button.onClick ShowCreateNew ] [ text "Add new domain role" ]
                    ]
            ]


viewSelectDomainRole : List DomainRole -> String -> Html Msg
viewSelectDomainRole added selected =
  let
    roles =
      [ ("Specification Model", "Produces a document describing a job/request that needs to be performed. Example: Advertising Campaign Builder")
      , ("Execution Model", "Performs or tracks a job. Example: Advertising Campaign Engine")
      , ("Audit Model", "Monitors the execution. Example: Advertising Campaign Analyser")
      , ("Approver", "Receives requests and determines if they should progress to the next step of the process. Example: Fraud Check")
      , ("Enforcer", "Ensures that other contexts carry out certain operations. Example: GDPR Context (ensures other contexts delete all of a user’s data)")
      , ("Octopus Enforcer", "Ensures that multiple/all contexts in the system all comply with a standard rule. Example: GDPR Context (as above)")
      , ("Interchanger", "Translates between multiple ubiquitous languages.")
      , ("Gateway", "Sits at the edge of a system and manages inbound and/or outbound communication. Example: IoT Message Gateway")
      , ("Gateway Interchange", "The combination of a gateway and an interchange.")
      , ("Dogfood Context", "Simulates the customer experience of using the core bounded contexts. Example: Whitelabel music store")
      , ("Bubble Context", "Sits in-front of legacy contexts providing a new, cleaner model while legacy contexts are being replaced.")
      , ("Autonomous Bubble", "Bubble context which has its own data store and synchronises data asynchronously with the legacy contexts.")
      , ("Brain Context (likely anti-pattern)", "Contains a large number of important rules and many other contexts depend on it. Example: rules engine containing all the domain rules")
      , ("Funnel Context", "Receives documents from multiple upstream contexts and passes them to a single downstream context in a standard format (after applying its own rules).")
      , ("Engagement Context", "Provides key features which attract users to keep using the product. Example: Free Financial Advice Context")
      ]
    addedIds = List.map DomainRoles.getName added
  in
    div [ class "pb-2"]
      [ div [ style "max-height" "300px", class "overflow-auto", class "pb-2" ]
        [ ListGroup.custom
            ( roles
                |> List.map(\(key, description) ->
                ListGroup.button
                    [ if List.member key addedIds
                      then ListGroup.disabled
                      else
                        if key == selected
                        then ListGroup.active
                        else ListGroup.attrs []
                    , ListGroup.attrs [ onClick(SelectRole key description) ]
                    ]
                    [ div []
                        [ Html.h6 [] [ text key]
                        , Html.small [] [text description]
                        ]
                    ]
                )
            )
        ]
      ]
