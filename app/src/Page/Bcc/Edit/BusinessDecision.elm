module Page.Bcc.Edit.BusinessDecision exposing (..)

import BoundedContext.BusinessDecision exposing (BusinessDecisions, BusinessDecision, getName, getDescription, defineBusinessDecision, addBusinessDecision, deleteBusinessDecision)
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
import BoundedContext.BusinessDecision exposing (BusinessDecision(..))
import BoundedContext.BusinessDecision exposing (Problem(..))
import BoundedContext.BusinessDecision exposing (getId)

type Msg
    = Show 
    | StartAddNew
    | AddNew BusinessDecision
    | ChangeDecisionName String
    | ChangeDecisionDescription String
    | CancelAdding
    | Delete String 

type ChangingModel
  = AddingNewBusinessDecision String String (Result BoundedContext.BusinessDecision.Problem BusinessDecision)

type alias Model =
  { decisions : BusinessDecisions
  , changingModel : Maybe ChangingModel
  }

init : BusinessDecisions -> Model
init decisions =
  { decisions = decisions
  , changingModel = Nothing
  }

update : Msg -> Model -> Model
update msg model =
    case (msg, model.changingModel) of
        (StartAddNew, _) ->
            { model | changingModel = Just <| AddingNewBusinessDecision "" "" (defineBusinessDecision model.decisions "" "") }

        (ChangeDecisionName name, Just (AddingNewBusinessDecision _ description _)) ->
            { model | changingModel = Just <| AddingNewBusinessDecision name description (defineBusinessDecision model.decisions name description) }

        (ChangeDecisionDescription description, Just (AddingNewBusinessDecision name _ _)) ->
            { model | changingModel = Just <| AddingNewBusinessDecision name description (defineBusinessDecision model.decisions name description) }

        (CancelAdding, _) ->
            { model | changingModel = Nothing}

        (AddNew decision, Just (AddingNewBusinessDecision _ _ _)) ->
            let
                newDecision = addBusinessDecision model.decisions decision

            in
                case newDecision of
                    Ok decisions ->
                        { model
                        | decisions = decisions
                        , changingModel = Nothing
                        }
                    Err _ -> 

                        model

        (Delete name, Nothing) ->
            { model | decisions = deleteBusinessDecision model.decisions name }
        
        _ -> model

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
    [ text (getName decision)
    , Button.button
      [ Button.secondary
      , Button.small
      , Button.onClick (Delete (decision |> getId))
      , Button.attrs [class "float-right"]
      ]
      [ text "X" ]
    ]
  , Html.dd
    []
    [ getDescription decision
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
                                        DefinitionEmpty -> "No decision name is specified"
                                        AlreadyExists -> "The business decision with name '" ++ name ++ "' has already been defined before. Please use a distinct, case insensitive name."
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