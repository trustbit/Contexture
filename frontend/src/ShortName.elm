module ShortName exposing(
  ShortName, Problem(..),
  fromString, toString,
  shortNameDecoder, shortNameEncoder)

import Json.Decode as Decode exposing(Decoder)
import Json.Encode as Encode


import Set exposing (Set)

type ShortName =
  ShortName String

type Problem
  = Empty
  | ExceedsMaxLength
  | StartsWithNumber
  | StartsWithHyphen
  | EndsWithHyphen
  | ContainsWhitespace
  | ContainsSpecialChars (Set Char)

invalidWhitespace : Set Char
invalidWhitespace =
  [ ' ', '\n', '\r' ]
  |> Set.fromList

isHyphen : Char -> Bool
isHyphen char =
  char == '-'

checkNonEmpty : List Char -> Result Problem ShortName
checkNonEmpty chars =
  let
    exceedsMaxLength =
      List.length chars > 16
    startsWithNumber =
      chars
      |> List.head
      |> Maybe.map Char.isDigit
      |> Maybe.withDefault False
    startsWithHyphen =
      chars
      |> List.head
      |> Maybe.map isHyphen
      |> Maybe.withDefault False
    endsWithHyphen =
      chars
      |> List.reverse
      |> List.head
      |> Maybe.map isHyphen
      |> Maybe.withDefault False
    containsWhitespaces =
      invalidWhitespace
      |> Set.intersect (Set.fromList chars)
      |> Set.isEmpty
      |> not
    specialChars =
      chars
      |> Set.fromList
      |> Set.filter (Char.isAlphaNum >> not)
      |> Set.filter (isHyphen >> not)

  in
    if exceedsMaxLength then
      Err ExceedsMaxLength
    else if startsWithNumber then
      Err StartsWithNumber
    else if startsWithHyphen then
      Err StartsWithHyphen
    else if endsWithHyphen then
      Err EndsWithHyphen
    else if containsWhitespaces then
      Err ContainsWhitespace
    else if not (Set.isEmpty specialChars) then
      Err <| ContainsSpecialChars specialChars
    else
      chars |> String.fromList |> ShortName |> Ok

fromString : String -> Result Problem ShortName
fromString potentialShortName =
  if potentialShortName |> String.isEmpty then
    Err Empty
  else
    potentialShortName
    |> String.toList
    |> checkNonEmpty

toString : ShortName -> String
toString (ShortName value) =
  value

shortNameDecoder : Decoder ShortName
shortNameDecoder =
  Decode.map ShortName Decode.string


shortNameEncoder : ShortName -> Encode.Value
shortNameEncoder (ShortName value) =
  Encode.string value
