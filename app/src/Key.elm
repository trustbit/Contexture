module Key exposing(
  Key, Problem(..),
  fromString, toString,
  keyDecoder, keyEncoder)

import Json.Decode as Decode exposing(Decoder)
import Json.Encode as Encode


import Set exposing (Set)

type Key =
  Key String

type Problem
  = Empty
  | StartsWithNumber
  | ContainsWhitespace
  | ContainsSpecialChars (Set Char)

invalidWhitespace : Set Char
invalidWhitespace =
  [ ' ', '\n', '\r' ]
  |> Set.fromList

checkNonEmpty : List Char -> Result Problem Key
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
      chars |> String.fromList |> Key |> Ok

fromString : String -> Result Problem Key
fromString potentialKey =
  if potentialKey |> String.isEmpty then
    Err Empty
  else
    potentialKey
    |> String.toList
    |> checkNonEmpty

toString : Key -> String
toString (Key value) =
  value

keyDecoder : Decoder Key
keyDecoder =
  Decode.map Key Decode.string


keyEncoder : Key -> Encode.Value
keyEncoder (Key value) =
  Encode.string value
