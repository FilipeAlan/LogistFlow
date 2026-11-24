# Glossary

| Term | Meaning in LogistFlow |
| --- | --- |
| Acknowledgement | A signal from a consumer telling RabbitMQ it can remove a message after successful processing or a durable handoff for retry. |
| At-least-once Delivery | A guarantee that a durably retained message reaches its consumer one or more times once the delivery path recovers. A lost acknowledgement can cause redelivery. |
| Atomicity | The property that all writes in a transaction take effect together or all are discarded. |
| Audit record | A record that Audit keeps of a confirmed business event, such as a delivery status change. |
| Backlog | Work awaiting processing. In the outbox, the pending message count and the age of the oldest message show its size and delay. |
| Business capability | Work that the platform takes responsibility for, such as Customer Support. |
| Commit | The database operation that makes a transaction's changes durable. |
| Customer Support | An internal LogistFlow capability that gives Customer Support Agents access to delivery information and history through its own read projection. |
| Customer Support Agent | A person outside the system boundary who uses Customer Support to look up a delivery and its history. |
| Dead Letter Queue | A queue that holds messages set aside after a permanent processing error or exhausted retries, for investigation and controlled reprocessing. |
| Delivery history | A chronological account of recorded changes to a delivery. |
| Delivery status | The current state of a delivery. A Driver can update it, and a Logistics Partner can query it. |
| Dual Write Problem | The risk that a database update succeeds while a separate message publication fails, or vice versa, leaving the two systems out of sync. |
| Durable Queue | A RabbitMQ queue whose definition survives a broker restart. Message persistence determines which queued messages can also be recovered. |
| Event-driven architecture | An approach in which an event records that something happened and other parts of the system can react to it. |
| External actor | A person, organization, or system outside the platform boundary that exchanges information with LogistFlow. |
| External API | An application programming interface (API) that LogistFlow exposes to other systems. Logistics partners use it to query delivery status. |
| Failure Isolation | Keeping a failure in one operation from disrupting independent work. Tracking can update its projection while Notifications retries a failed send. |
| Idempotency Key | A client-generated identifier reused for every retry of one logical API operation so the server can recognize the request and return its recorded result. |
| Idempotent Consumer | A consumer that recognizes repeated deliveries of the same event and prevents duplicate business effects in the state it controls. |
| Offline-First | A client design that records pending operations locally while disconnected and synchronizes them when connectivity returns. |
| Persistent Message | A message marked for durable storage by RabbitMQ so it can survive a broker restart in a durable queue. |
| Publisher Confirm | A RabbitMQ response to a publisher indicating whether the broker accepted a publication. The publisher must also check for unroutable messages. |
| Retry with Backoff | Retrying a transiently failed operation after increasing delays to give the dependency time to recover. |
| Rollback | The database operation that discards a transaction's uncommitted changes. |
| System boundary | The dividing line between LogistFlow's responsibilities and the external actors and systems it interacts with. |
| Transactional Outbox | A pattern that writes business state and its outgoing events in one database transaction. A worker later publishes the recorded events. |
| Transient Failure | A temporary fault, such as a network interruption, after which the same operation may succeed on retry. |
