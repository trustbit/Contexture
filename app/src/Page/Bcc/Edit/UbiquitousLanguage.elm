module Page.Bcc.Edit.UbiquitousLanguage exposing (Model, Msg, init, update, view)

import Html exposing (Html, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick, onSubmit)

import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input
import Bootstrap.Form.Textarea as Textarea
import Bootstrap.Card as Card
import Bootstrap.Card.Block as Block
import Bootstrap.Button as Button
import Bootstrap.Text as Text

import BoundedContext.UbiquitousLanguage as UbiquitousLanguage exposing (UbiquitousLanguage, LanguageTerm, DomainTermId)

type ChangingModel
  = AddingNewTerm String String (Result UbiquitousLanguage.Problem LanguageTerm)

type alias Model =
  { language : UbiquitousLanguage
  , changeModel : Maybe ChangingModel
  }

init : UbiquitousLanguage -> Model
init language =
  { language = language
  , changeModel = Nothing
  }


type Msg
  = StartToDefineNewTerm
  | ChangeDomainTerm String
  | ChangeDescription String
  | Save LanguageTerm
  | DeleteTerm DomainTermId


update : Msg -> Model -> Model
update msg model =
  case (msg, model.changeModel) of
    (StartToDefineNewTerm, _) ->
      { model | changeModel = Just <| AddingNewTerm "" "" (UbiquitousLanguage.defineLanguageTerm model.language "" "") }

    (ChangeDomainTerm term, Just (AddingNewTerm _ description _)) ->
      { model | changeModel = Just <| AddingNewTerm term description (UbiquitousLanguage.defineLanguageTerm model.language term description) }

    (ChangeDescription description, Just (AddingNewTerm term _ _)) ->
      { model | changeModel = Just <| AddingNewTerm term description (UbiquitousLanguage.defineLanguageTerm model.language term description) }

    (Save term, Just (AddingNewTerm _ _ _)) ->
      let
        newLanguage = UbiquitousLanguage.addLanguageTerm model.language term

      in
        case newLanguage of
          Ok terms ->
            { model
            | language = terms
            , changeModel = Nothing
            }
          Err _ ->
            model
    (DeleteTerm term, Nothing) ->
      { model | language = UbiquitousLanguage.removeLanguageTerm model.language term }

    _ ->
      model

view : Model -> Html Msg
view = viewAsDl

viewCard : Model -> Html Msg
viewCard model =
  viewDefineTerm model.changeModel :: (
    model.language
    |> UbiquitousLanguage.languageTerms
    |> List.map viewLanguageTerm
  )
  |> Card.columns


viewDefineTerm : Maybe ChangingModel -> Card.Config Msg
viewDefineTerm model =
  case model of
     Just (AddingNewTerm term description definition) ->
      let
        (termIsValid, anEvent, feedbackText) =
          case definition of
            Ok d ->
              (True, [ onSubmit (Save d) ], "")
            Err p ->
              let
                errorText =
                  case p of
                    UbiquitousLanguage.TermDefinitionEmpty ->
                      "No domain term is specified"
                    UbiquitousLanguage.TermAlreadyAdded ->
                      "The term '" ++ term ++ "' was already added. Please use a distinct, case insensitive name."
              in
                (False, [], errorText)
      in
        Card.config [ Card.attrs [ class "mb-3", class "shadow" ] ]
        |> Card.block []
          [ Block.custom <|
            Form.form anEvent
            [ Form.group []
              [ Form.label [ for "term" ] [ text "Term" ]
              , Input.text
                [ Input.id "term"
                , Input.value term
                , Input.placeholder "Domain-Term"
                , Input.onInput ChangeDomainTerm
                , if termIsValid
                  then Input.success
                  else Input.danger
                ]
              , Form.invalidFeedback [] [ text feedbackText]
              ]
            , Form.group []
              [ Form.label [ for "description" ] [ text "Description" ]
              , Textarea.textarea
                [ Textarea.id "description"
                , Textarea.value description
                -- , Textarea.placeholder "Description"
                , Textarea.onInput ChangeDescription
                ]
              ]
            , Button.submitButton [ Button.primary, Button.disabled (not termIsValid) ] [ text "Define new Term" ]
            ]
          ]
     _ ->
      Card.config [ Card.attrs [ class "mb-3", class "shadow" ], Card.align Text.alignXsCenter ]
      |> Card.block []
        [ Block.custom
          <| Button.button [ Button.primary, Button.onClick StartToDefineNewTerm ] [ text "Define new term" ]
        ]


viewLanguageTerm : LanguageTerm -> Card.Config Msg
viewLanguageTerm model =
  Card.config [ Card.attrs [ class "mb-3", class "shadow" ] ]
    |> Card.block []
      [ Block.titleH6 []
        [ text (model |> UbiquitousLanguage.domainTerm)]
      , Block.text [ class "text-muted"]
        [ model
        |> UbiquitousLanguage.domainDescription
        |> Maybe.map text
        |> Maybe.withDefault (Html.i [] [ text "No description :-(" ])
        ]
      ]


viewDlTerm : LanguageTerm -> List (Html Msg)
viewDlTerm model =
  [ Html.dt []
    [ text (model |> UbiquitousLanguage.domainTerm)
    , Button.button
      [ Button.secondary
      , Button.small
      , Button.onClick (DeleteTerm (model |> UbiquitousLanguage.id))
      , Button.attrs [class "float-right"]
      ]
      [ text "X" ]
    ]
  , Html.dd
    []
    [ model
      |> UbiquitousLanguage.domainDescription
      |> Maybe.map text
      |> Maybe.withDefault (Html.i [] [ text "No description :-(" ])
    ]
  ]

viewAsDl: Model -> Html Msg
viewAsDl model =
  Html.div []
  [ viewDefineTerm model.changeModel  |> Card.view
  , Html.dl []
    ( model.language
    |> UbiquitousLanguage.languageTerms
    |> List.concatMap viewDlTerm
    )
  ]

