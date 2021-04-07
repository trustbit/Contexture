module Api exposing (
  Endpoint, Configuration, ApiResponse, ApiResult, Include(..), Expand(..),
  domains, domain, subDomains,
  allBoundedContexts, boundedContexts, boundedContext,
  collaborations, collaboration,
  canvas, 
  url, config, baseConfig)

import Http
import Url
import Url.Builder exposing (QueryParameter)

import Domain.DomainId as Domain exposing (DomainId)
import BoundedContext.BoundedContextId as BoundedContext exposing (BoundedContextId)
import ContextMapping.CollaborationId as ContextMapping exposing(CollaborationId)

type Configuration
  = Configuration String

type alias PathSegment = String

type Endpoint
  = Endpoint (List PathSegment) (List QueryParameter)

type alias ApiResponse model = Result Http.Error model 
type alias ApiResult model msg = (ApiResponse model-> msg) -> Cmd msg

withoutQuery : List String -> Endpoint
withoutQuery segments =
  Endpoint segments []

withQuery : List String -> List QueryParameter -> Endpoint
withQuery segments query =
  Endpoint segments query

config : Url.Url -> Configuration
config base =
  Configuration (base |> Url.toString)


baseConfig : String -> Configuration
baseConfig base =
  Configuration base

url : Configuration -> Endpoint -> String
url (Configuration basePath) (Endpoint segments query) =
  basePath ++ (Url.Builder.absolute segments query)

type Include
  = Subdomains
  | BoundedContexts

type Expand
  = Domain

domains : List Include -> Endpoint
domains include =
  withQuery [ "domains" ] (include |> includeInRequest)

domain : List Include -> DomainId -> Endpoint
domain include domainId =
  withQuery [ "domains", Domain.idToString domainId ] (include |> includeInRequest)

subDomains : List Include -> DomainId -> Endpoint
subDomains include domainId =
  withQuery [ "domains", Domain.idToString domainId, "domains" ] (include |> includeInRequest)

boundedContexts : DomainId -> Endpoint
boundedContexts domainId =
   withoutQuery [ "domains", Domain.idToString domainId, "boundedContexts" ]

allBoundedContexts : List Expand -> Endpoint 
allBoundedContexts expand =
  withQuery [ "boundedContexts" ] ( expand |> expandInRequest)

boundedContext : BoundedContextId -> Endpoint
boundedContext context =
  withoutQuery [ "boundedContexts", BoundedContext.idToString context ]

canvas : BoundedContextId -> Endpoint
canvas context =
  withQuery [ "boundedContexts", BoundedContext.idToString context ] (expandInRequest [ Domain ])

collaborations : Endpoint
collaborations =
  withoutQuery [ "collaborations"]


collaboration : CollaborationId -> Endpoint
collaboration id =
  withoutQuery [ "collaborations", ContextMapping.idToString id]


includeInRequest : List Include -> List QueryParameter
includeInRequest include =
  include
  |> List.map
    ( \i ->
        case i of
          Subdomains -> Url.Builder.string "_embed" "domains"
          BoundedContexts -> Url.Builder.string "_embed" "boundedContexts" 
    )

expandInRequest : List Expand -> List QueryParameter
expandInRequest expand =
  expand
  |> List.map
    ( \e ->
      case e of
         Domain -> Url.Builder.string "_expand" "domain")