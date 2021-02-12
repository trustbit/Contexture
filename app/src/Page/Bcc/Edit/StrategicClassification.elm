module Page.Bcc.Edit.StrategicClassification exposing (..)

import Html exposing (Html, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick, onSubmit)

import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Form as Form
import Bootstrap.Form.Radio as Radio
import Bootstrap.Form.Checkbox as Checkbox
import Bootstrap.Card as Card
import Bootstrap.Card.Block as Block
import Bootstrap.Button as Button
import Bootstrap.ButtonGroup as ButtonGroup
import Bootstrap.Text as Text
import Bootstrap.Utilities.Spacing as Spacing
import Bootstrap.Utilities.Flex as Flex

import Api

import BoundedContext.BoundedContextId exposing(BoundedContextId)
import BoundedContext.StrategicClassification as StrategicClassification exposing (StrategicClassification)
import Html
import RemoteData


type alias Model =
  { classification : RemoteData.WebData StrategicClassification
  , changingClassification : Maybe StrategicClassification
  , config : Api.Configuration
  , boundedContextId : BoundedContextId
  }


init : Api.Configuration -> BoundedContextId -> StrategicClassification -> (Model, Cmd Msg)
init configuration id model =
    ( { classification = RemoteData.succeed model
      , changingClassification = Nothing
      , config = configuration
      , boundedContextId = id
      }
    , Cmd.none
    )


type Action t
  = Add t
  | Remove t


type Msg
  = Saved (Api.ApiResponse StrategicClassification)
  | StartChanging
  | SetDomainType StrategicClassification.DomainType
  | ChangeBusinessModel (Action StrategicClassification.BusinessModel)
  | SetEvolution StrategicClassification.Evolution
  | SaveClassification StrategicClassification
  | CancelChanging


updateClassification : (StrategicClassification -> StrategicClassification) -> Model -> Model
updateClassification updater model =
  { model | changingClassification = model.changingClassification |> Maybe.map updater }

noCommand model = (model, Cmd.none)

update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case msg of
    StartChanging ->
      noCommand { model | changingClassification = model.classification |> RemoteData.toMaybe }

    CancelChanging ->
      noCommand { model | changingClassification = Nothing }

    SaveClassification classification ->
      ( model
      , StrategicClassification.reclassify model.config model.boundedContextId classification Saved)

    Saved (Ok classification) ->
      noCommand
        { model
        | classification = RemoteData.succeed classification
        , changingClassification = Nothing
        }

    Saved (Err error) ->
      Debug.todo "Error"
      noCommand <| Debug.log "Error on save " model

    SetDomainType class ->
      model
      |> updateClassification (\c -> { c | domain = Just class })
      |> noCommand

    ChangeBusinessModel (Add business) ->
      model
      |> updateClassification (\c -> { c | business = business :: c.business})
      |> noCommand

    ChangeBusinessModel (Remove business) ->
      model
      |> updateClassification (\c -> { c | business = c.business |> List.filter (\bm -> bm /= business )})
      |> noCommand

    SetEvolution evo ->
      model
      |> updateClassification (\c -> { c | evolution = Just evo })
      |> noCommand




viewClassification : StrategicClassification -> Html Msg
viewClassification classification =
  let
    domainDescriptions =
      [ StrategicClassification.Core, StrategicClassification.Supporting, StrategicClassification.Generic ]
      |> List.map StrategicClassification.domainDescription
      |> List.map (\d -> (d.name, d.description))
    businessDescriptions =
      [ StrategicClassification.Revenue, StrategicClassification.Engagement, StrategicClassification.Compliance, StrategicClassification.CostReduction ]
      |> List.map StrategicClassification.businessDescription
      |> List.map (\d -> (d.name, d.description))
    evolutionDescriptions =
      [ StrategicClassification.Genesis, StrategicClassification.CustomBuilt, StrategicClassification.Product, StrategicClassification.Commodity ]
      |> List.map StrategicClassification.evolutionDescription
      |> List.map (\d -> (d.name, d.description))
  in
    Grid.simpleRow
    [ Grid.col []
      [ viewLabel "classification" "Domain"
      , div []
          ( Radio.radioList "classification"
            [ viewRadioButton "core" classification.domain StrategicClassification.Core SetDomainType StrategicClassification.domainDescription
            , viewRadioButton "supporting" classification.domain StrategicClassification.Supporting SetDomainType StrategicClassification.domainDescription
            , viewRadioButton "generic" classification.domain StrategicClassification.Generic SetDomainType StrategicClassification.domainDescription
            -- TODO: Other
            ]
          )
        , viewDescriptionList domainDescriptions Nothing
          |> viewInfoTooltip "How important is this context to the success of your organisation?"
        ]
      , Grid.col []
        [ viewLabel "businessModel" "Business Model"
        , div []
            [ viewCheckbox "revenue" StrategicClassification.businessDescription StrategicClassification.Revenue classification.business
            , viewCheckbox "engagement" StrategicClassification.businessDescription StrategicClassification.Engagement classification.business
            , viewCheckbox "Compliance" StrategicClassification.businessDescription StrategicClassification.Compliance classification.business
            , viewCheckbox "costReduction" StrategicClassification.businessDescription StrategicClassification.CostReduction classification.business
            -- TODO: Other
            ]
            |> Html.map ChangeBusinessModel

        , viewDescriptionList businessDescriptions Nothing
          |> viewInfoTooltip "What role does the context play in your business model?"
        ]
      , Grid.col []
        [ viewLabel "evolution" "Evolution"
        , div []
            ( Radio.radioList "evolution"
              [ viewRadioButton "genesis" classification.evolution StrategicClassification.Genesis SetEvolution StrategicClassification.evolutionDescription
              , viewRadioButton "customBuilt" classification.evolution StrategicClassification.CustomBuilt SetEvolution StrategicClassification.evolutionDescription
              , viewRadioButton "product" classification.evolution StrategicClassification.Product SetEvolution StrategicClassification.evolutionDescription
              , viewRadioButton "commodity" classification.evolution StrategicClassification.Commodity SetEvolution StrategicClassification.evolutionDescription
              -- TODO: Other
              ]
            )
          , viewDescriptionList evolutionDescriptions Nothing
          |> viewInfoTooltip "How evolved is the concept (see Wardley Maps)"
        ]
      ]


view : Model -> Html Msg
view model =
  div []
    ( case model.changingClassification of
        Just classification ->
          [ Grid.row []
            [ Grid.col []
              [ viewCaption
                [ text "Strategic Classification"
                , ButtonGroup.buttonGroup []
                  [ ButtonGroup.button [ Button.primary, Button.small, Button.onClick (SaveClassification classification)] [text "Reclassify"]
                  , ButtonGroup.button [ Button.secondary, Button.small, Button.onClick CancelChanging] [text "X"]
                  ]
                ]
              ]
            ]
          , Html.fieldset [] [ viewClassification classification ]
          ]

        Nothing ->
          [ Grid.row []
            [ Grid.col []
              [ viewCaption
                [ text "Strategic Classification"
                , Button.button [ Button.outlinePrimary, Button.small, Button.onClick StartChanging] [text "Start Classification"]
                ]
              ]
            ]
          , Html.fieldset [ attribute "disabled" "disabled"] 
            [ case model.classification of
                RemoteData.Success classification ->
                  viewClassification classification
                _ ->
                  text "Loading"
            ]
          ]
      )


viewCaption : List(Html msg) -> Html msg
viewCaption content =
  Form.label
    [ Flex.justifyBetween
    , Flex.block
    , style "background-color" "lightGrey"
    , Spacing.p2
    ]
    content


viewRadioButton : String  -> Maybe value -> value -> (value -> m) -> (value -> StrategicClassification.Description) -> Radio.Radio m
viewRadioButton id currentValue option toMsg toTitle =
  Radio.createAdvanced
    [ Radio.id id, Radio.onClick (toMsg option), Radio.checked (currentValue == Just option) ]
    (Radio.label [] [ text (toTitle option).name ])


viewCheckbox : String -> (value -> StrategicClassification.Description) -> value -> List value -> Html (Action value)
viewCheckbox id description value currentValues =
  Checkbox.checkbox
    [Checkbox.id id
    , Checkbox.onCheck(\isChecked -> if isChecked then Add value else Remove value )
    , Checkbox.checked (List.member value currentValues)
    ]
    (description value).name


viewLabel : String -> String -> Html msg
viewLabel labelId caption =
  Form.label
    [ for labelId ]
    [ Html.b [] [ text caption ] ]


viewInfoTooltip : String -> Html msg -> Html msg
viewInfoTooltip title description =
  Form.help []
    [ Html.details []
      [ Html.summary []
        [ text title ]
      , Html.p [ ] [ description ]
      ]
    ]


viewDescriptionList : List (String, String) -> Maybe String -> Html msg
viewDescriptionList model sourceReference =
  let
    footer =
      case sourceReference of
        Just reference ->
          [ Html.footer
            [ class "blockquote-footer"]
            [ Html.a
              [target "_blank"
              , href reference
              ]
              [ text "Source of the descriptions"]
            ]
          ]
        Nothing -> []
  in
    Html.dl []
      ( model
        |> List.concatMap (
          \(t, d) ->
            [ Html.dt [] [ text t ]
            , Html.dd [] [ text d ]
            ]
        )
      )
    :: footer
    |> div []


