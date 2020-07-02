module Api exposing (
  Endpoint, Configuration,
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


domains : Endpoint
domains =
  withoutQuery [ "domains" ]

domain : DomainId -> Endpoint
domain domainId =
  withoutQuery [ "domains", Domain.idToString domainId ]

subDomains : DomainId -> Endpoint
subDomains domainId =
  withoutQuery [ "domains", Domain.idToString domainId, "domains" ]

boundedContexts : DomainId -> Endpoint
boundedContexts domainId =
   withoutQuery [ "domains", Domain.idToString domainId, "bccs" ]

boundedContext : BoundedContextId -> Endpoint
boundedContext context =
  withoutQuery [ "bccs", BoundedContext.idToString context ]