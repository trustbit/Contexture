module Page.Bcc.Edit.BusinessDecision exposing (..)


import Page.Bcc.Edit.Dependencies exposing (Msg)
import Html exposing (Html, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick, onSubmit)

import Bootstrap.Button as Button
import Bootstrap.Card as Card
import Bootstrap.Card.Block as Block
import Bootstrap.Text as Text
import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input
import Bootstrap.Form.Textarea as Textarea

import Api

import BoundedContext.BusinessDecision as BusinessDecision exposing (BusinessDecisions, BusinessDecision, Problem)
import BoundedContext.BoundedContextId exposing (BoundedContextId)

type Msg
    = Show 
    | StartAddNew
    | AddNew BusinessDecision
    | ChangeDecisionName String
    | ChangeDecisionDescription String
    | CancelAdding
    | Delete String 
    | Loaded (Api.ApiResponse BusinessDecisions)
    | DecisionAdded (Api.ApiResponse BusinessDecisions)
    | DecisionRemoved (Api.ApiResponse BusinessDecisions)


type ChangingModel
  = AddingNewBusinessDecision String String (Result Problem BusinessDecision)

type alias Model =
  { decisions : BusinessDecisions
  , changingModel : Maybe ChangingModel
  , config : Api.Configuration
  , boundedContextId : BoundedContextId
  }

init : Api.Configuration -> BoundedContextId  -> (Model, Cmd Msg)
init config id =
    ( { decisions = []
      , changingModel = Nothing
      , config = config
      , boundedContextId = id
      }
    , BusinessDecision.getBusinessDecisions config id Loaded
    )

noCommand model = (model, Cmd.none)

update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
    case (msg, model.changingModel) of
        (StartAddNew, _) ->
            noCommand { model | changingModel = Just <| AddingNewBusinessDecision "" "" (BusinessDecision.defineBusinessDecision model.decisions "" "") }

        (ChangeDecisionName name, Just (AddingNewBusinessDecision _ description _)) ->
            noCommand { model | changingModel = Just <| AddingNewBusinessDecision name description (BusinessDecision.defineBusinessDecision model.decisions name description) }

        (ChangeDecisionDescription description, Just (AddingNewBusinessDecision name _ _)) ->
            noCommand { model | changingModel = Just <| AddingNewBusinessDecision name description (BusinessDecision.defineBusinessDecision model.decisions name description) }

        (CancelAdding, _) ->
            noCommand { model | changingModel = Nothing}

        (AddNew decision, Just (AddingNewBusinessDecision _ _ _)) ->
            let
                newDecisions = BusinessDecision.addBusinessDecision model.config model.boundedContextId model.decisions decision
            in
                case newDecisions of
                Ok decisions ->
                    ({ model | changingModel = Nothing }, decisions DecisionAdded)
                Err _ ->
                    noCommand model

        (Delete name, Nothing) ->
            (model, BusinessDecision.removeBusinessDecision model.config model.boundedContextId model.decisions name DecisionRemoved)

        (Loaded (Ok decisions),_) ->
            noCommand { model | decisions = decisions}
        (DecisionAdded (Ok decisions),_) ->
            noCommand { model | decisions = decisions}
        (DecisionRemoved (Ok decisions),_) ->
            noCommand { model | decisions = decisions}
        
        _ -> 
            noCommand model

view : Model -> Html Msg
view model =
    Html.div []
    [   viewAddDecision model.changingModel |> Card.view
    ,   Html.dl []
        (
            List.map viewDecision model.decisions |> List.concat
        )
    ]

viewDecision : BusinessDecision -> List (Html Msg)
viewDecision decision =
  [ Html.dt []
    [ text (BusinessDecision.getName decision)
    , Button.button
      [ Button.secondary
      , Button.small
      , Button.onClick (Delete (decision |> BusinessDecision.getId))
      , Button.attrs [class "float-right"]
      ]
      [ text "X" ]
    ]
  , Html.dd
    []
    [ BusinessDecision.getDescription decision
      |> Maybe.map text
      |> Maybe.withDefault (Html.i [] [ text "No description :-(" ])
    ]
  ]

viewAddDecision : Maybe ChangingModel -> Card.Config Msg
viewAddDecision model =
    case model of
        Just (AddingNewBusinessDecision name description result) ->
            let
                (nameIsValid, anEvent, feedbackText) =
                    case result of
                        Ok d ->
                            (True, [ onSubmit (AddNew d) ], "")
                        Err p ->
                            let
                                errorText =
                                    case p of
                                        BusinessDecision.DefinitionEmpty -> "No decision name is specified"
                                        BusinessDecision.AlreadyExists -> "The business decision with name '" ++ name ++ "' has already been defined before. Please use a distinct, case insensitive name."
                            in
                            (False, [], errorText)
            in
                Card.config [ Card.attrs [ class "mb-3", class "shadow" ] ]
                |> Card.block []
                [ Block.custom <|
                    Form.form anEvent
                    [ Form.group []
                    [ Form.label [ for "name" ] [ text "Business decision name" ]
                    , Input.text
                        [ Input.id "name"
                        , Input.value name
                        , Input.onInput ChangeDecisionName
                        , if nameIsValid
                            then Input.success
                            else Input.danger
                        ]
                    , Form.help [] [ text "The business decision name that is used inside this bounded context." ]
                    , Form.invalidFeedback [] [ text feedbackText]
                    ]
                    , Form.group []
                    [ Form.label [ for "description" ] [ text "Description" ]
                    , Textarea.textarea
                        [ Textarea.id "description"
                        , Textarea.value description
                        , Textarea.onInput ChangeDecisionDescription
                        ]
                    , Form.help [] [ text "Define the meaning of the this business decision inside this bounded context." ]
                    ]
                    , Button.button [ Button.outlineSecondary, Button.onClick CancelAdding, Button.attrs [ class "mr-2"] ] [ text "Cancel"]
                    , Button.submitButton [ Button.primary, Button.disabled (not nameIsValid) ] [ text "Add new business decision" ]
                    ]
                ]
        _ ->
            Card.config [ Card.attrs [ class "mb-3", class "shadow" ], Card.align Text.alignXsCenter ]
                |> Card.block []
            [ Block.custom <| Button.button [ Button.primary, Button.onClick StartAddNew ] [ text "Add new business decision" ]]