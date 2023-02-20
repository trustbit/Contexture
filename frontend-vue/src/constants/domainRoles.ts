import { DomainRole } from "~/types/boundedContext";

export const predefinedDomainRoles: DomainRole[] = [
  {
    name: "Specification Model",
    description:
      "Produces a document describing a job/request that needs to be performed. Example: Advertising Campaign Builder",
  },
  {
    name: "Execution Model",
    description: "Performs or tracks a job. Example: Advertising Campaign Engine",
  },
  {
    name: "Audit Model",
    description: "Monitors the execution. Example: Advertising Campaign Analyser",
  },
  {
    name: "Approver",
    description:
      "Receives requests and determines if they should progress to the next step of the process. Example: Fraud Check",
  },
  {
    name: "Enforcer",
    description:
      "Ensures that other contexts carry out certain operations. Example: GDPR Context (ensures other contexts delete all of a user’s data)",
  },
  {
    name: "Octopus Enforcer",
    description:
      "Ensures that multiple/all contexts in the system all comply with a standard rule. Example: GDPR Context (as above)",
  },
  {
    name: "Interchanger",
    description: "Translates between multiple ubiquitous languages.",
  },
  {
    name: "Gateway",
    description:
      "Sits at the edge of a system and manages inbound and/or outbound communication. Example: IoT Message Gateway",
  },
  {
    name: "Gateway Interchange",
    description: "The combination of a gateway and an interchange.",
  },
  {
    name: "Dogfood Context",
    description:
      "Simulates the customer experience of using the core bounded contexts. Example: Whitelabel music store",
  },
  {
    name: "Bubble Context",
    description:
      "Sits in-front of legacy contexts providing a new, cleaner model while legacy contexts are being replaced.",
  },
  {
    name: "Autonomous Bubble",
    description:
      "Bubble context which has its own data store and synchronises data asynchronously with the legacy contexts.",
  },
  {
    name: "Brain Context (likely anti-pattern)",
    description:
      "Contains a large number of important rules and many other contexts depend on it. Example: rules engine containing all the domain rules",
  },
  {
    name: "Funnel Context",
    description:
      "Receives documents from multiple upstream contexts and passes them to a single downstream context in a standard format (after applying its own rules).",
  },
  {
    name: "Engagement Context",
    description:
      "Provides key features which attract users to keep using the product. Example: Free Financial Advice Context",
  },
];
