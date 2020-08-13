module Page.Bcc.Edit.UbiquitousLanguage exposing (..)

import Html exposing (Html, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick, onSubmit)

import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input
import Bootstrap.Form.Textarea as Textarea
import Bootstrap.Form.Radio as Radio
import Bootstrap.Form.Checkbox as Checkbox
import Bootstrap.Card as Card
import Bootstrap.Card.Block as Block
import Bootstrap.Button as Button
import Bootstrap.Text as Text
import Bootstrap.Utilities.Spacing as Spacing
import Bootstrap.Utilities.Display as Display

import BoundedContext.UbiquitousLanguage as UbiquitousLanguage exposing (UbiquitousLanguage,LanguageTerm)
import BoundedContext.UbiquitousLanguage exposing (description)

type alias NewTerm = 
  { domainTerm : String
  , description : String  
  }
type ChangingModel
  = AddingNewTerm String String

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
  | Save
  | DeleteTerm String


update : Msg -> Model -> Model
update msg model =
  case (msg, model.changeModel) of
    (StartToDefineNewTerm, _) ->
      { model | changeModel = Just <| AddingNewTerm "" "" }
    
    (ChangeDomainTerm term, Just (AddingNewTerm _ description)) ->
      { model | changeModel = Just <| AddingNewTerm term description }

    (ChangeDescription description, Just (AddingNewTerm term _)) ->
      { model | changeModel = Just <| AddingNewTerm term description }

    (Save, Just (AddingNewTerm term description)) ->
      let
        newLanguage = UbiquitousLanguage.addLanguageTerm model.language term description

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
      { model | language = UbiquitousLanguage.removeLanguageTerm model.language  term }

    _ ->
      model

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
     Just (AddingNewTerm term description) ->
      Card.config [ Card.attrs [ class "mb-3", class "shadow" ] ]
      |> Card.block []
        [ Block.custom <|
          Form.form [ onSubmit Save ]
          [ Form.group []
            [ Form.label [ for "term" ] [ text "Term" ]
            , Input.text 
              [ Input.id "term"
              , Input.value term
              , Input.placeholder "Domain-Term"
              , Input.onInput ChangeDomainTerm
              ]
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
          , Button.submitButton [ Button.primary] [ text "Define new Term" ]
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
        |> UbiquitousLanguage.description
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
      , Button.onClick (DeleteTerm (model |> UbiquitousLanguage.domainTerm))
      , Button.attrs [class "float-right"]
      ]
      [ text "X" ]
    ]
  , Html.dd 
    [] 
    [ model
      |> UbiquitousLanguage.description
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
  
