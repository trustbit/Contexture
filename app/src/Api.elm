module Api exposing (
  Endpoint, Configuration, ApiResponse, ApiResult, Include(..),
  domains, domain, subDomains,
  boundedContexts, boundedContext,
  url, config, configFromScoped)

import Http
import Url
import Url.Builder exposing (QueryParameter)

import Domain.DomainId as Domain exposing (DomainId)
import BoundedContext.BoundedContextId as BoundedContext exposing (BoundedContextId)

type Configuration
  = Configuration Url.Url

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

-- TODO: this is only temporary
configFromScoped : Url.Url -> Configuration
configFromScoped base =
  Configuration { base | path = "/api" }

config : Url.Url -> Configuration
config base =
  Configuration base

url : Configuration -> Endpoint -> Url.Url
url (Configuration baseUrl) (Endpoint segments query) =
  { baseUrl | path = baseUrl.path ++ Url.Builder.absolute segments query }

type Include
  = Subdomains
  | BoundedContexts

domains : List Include -> Endpoint
domains include =
  withQuery [ "domains" ] (include |> incudeInRequest)

domain : List Include -> DomainId -> Endpoint
domain include domainId =
  withQuery [ "domains", Domain.idToString domainId ] (include |> incudeInRequest)

subDomains : List Include -> DomainId -> Endpoint
subDomains include domainId =
  withQuery [ "domains", Domain.idToString domainId, "domains" ] (include |> incudeInRequest)

boundedContexts : DomainId -> Endpoint
boundedContexts domainId =
   withoutQuery [ "domains", Domain.idToString domainId, "bccs" ]

boundedContext : BoundedContextId -> Endpoint
boundedContext context =
  withoutQuery [ "bccs", BoundedContext.idToString context ]

incudeInRequest : List Include -> List QueryParameter
incudeInRequest include =
  include
  |> List.map
    ( \i ->
        case i of
          Subdomains -> Url.Builder.string "_embed" "domains"
          BoundedContexts -> Url.Builder.string "_embed" "bccs" 
    )