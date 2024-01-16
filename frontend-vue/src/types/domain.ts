import { BoundedContext } from "~/types/boundedContext";

export type DomainId = string;

export interface Domain {
  id: DomainId;
  parentDomainId?: DomainId;
  shortName?: string;
  name: string;
  vision?: string;
  subdomains: Domain[];
  boundedContexts: BoundedContext[];
}

export interface CreateDomain {
  name: String;
  shortName?: String;
  vision?: String;
}

export interface UpdateDomain {
  key?: string;
  name?: string;
  vision?: string;
}
