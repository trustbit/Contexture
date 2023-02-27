import { Domain, DomainId } from "~/types/domain";
import { Namespace } from "~/types/namespace";

export type BoundedContextId = string;
export type MessagesConsumedKeys = keyof MessagesConsumed;
export type MessageProduceKeys = keyof MessagesProduced;

export interface BoundedContext {
  id: BoundedContextId;
  parentDomainId: DomainId;
  shortName?: string;
  name: string;
  description?: string;
  classification: Classification;
  businessDecisions: BusinessDecision[];
  ubiquitousLanguage: UbiquitousLanguage;
  messages?: Messages;
  domainRoles?: DomainRole[];
  namespaces: Namespace[];
  domain: Domain;
}

export enum DomainType {
  Core = "Core",
  Supporting = "Supporting",
  Generic = "Generic",
}

export enum BusinessModel {
  Revenue = "Revenue",
  Engagement = "Engagement",
  Compliance = "Compliance",
  CostReduction = "CostReduction",
}

export enum Evolution {
  Genesis = "Genesis",
  CustomBuilt = "CustomBuilt",
  Product = "Product",
  Commodity = "Commodity",
}

export interface Classification {
  domainType?: DomainType;
  businessModel?: BusinessModel[];
  evolution?: Evolution;
}

export interface BusinessDecision {
  name: string;
  description?: string;
}

export interface UbiquitousLanguage {
  [key: string]: UbiquitousLanguageItem;
}

export interface UbiquitousLanguageItem {
  term: string;
  description?: string;
}

export interface Messages extends MessagesConsumed, MessagesProduced {}

export interface MessagesConsumed {
  commandsHandled: string[];
  eventsHandled: string[];
  queriesHandled: string[];
}

export interface MessagesProduced {
  commandsSent: string[];

  eventsPublished: string[];

  queriesInvoked: string[];
}

export interface DomainRole {
  name: string;
  description: string;
}

export interface CreateBoundedContext {
  name: String;
}

export interface CreateMessage {
  name: string;
}
