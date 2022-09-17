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
  | StartsWithNumber
  | ContainsWhitespace
  | ContainsSpecialChars (Set Char)

invalidWhitespace : Set Char
invalidWhitespace =
  [ ' ', '\n', '\r' ]
  |> Set.fromList

checkNonEmpty : List Char -> Result Problem ShortName
checkNonEmpty chars =
  let
    startsWithNumber =
      chars
      |> List.head
      |> Maybe.map Char.isDigit
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

  in
    if startsWithNumber then
      Err StartsWithNumber
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
