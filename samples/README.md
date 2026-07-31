# Samples

## Contoso.OrderService

The preview demo target. It is an ordinary .NET service, not a test probe: public namespaced types, ordinary static
fields, and an object graph shaped the way application code is shaped. That is the point — the demo should evaluate
expressions a reader would type against their own application.

The service reaches one deterministic stalled state and parks there:

- `ProcessedOrderCount` is 84,213 — it did most of its work before stopping.
- One batch, `batch-2026-07-30-0042`, still has 96 orders pending for hub `AMS-3`.
- Four hand-off attempts failed with `carrier-handoff-timeout`.
- `AssignedCarrier` is null, because no carrier ever accepted the batch. That null is the answer to "why".
- The main thread is parked in `DispatchLoop.AwaitCarrierAssignment`; two background workers are parked in
  `CarrierGateway.PollForAssignment`.

Every value is fixed, so a dump captured on any machine answers the same expressions the same way. The process prints
`READY` on stdout once it is stalled, which is how the demo script and the integration test know when to capture.

It builds optimized with a Portable PDB, because that is what a production dump is taken from.

### Running it

You do not normally run this directly. [`eng/Invoke-PreviewDemo.ps1`](../eng/Invoke-PreviewDemo.ps1) starts it,
captures a full dump, and replays [`eng/demo-session.pi`](../eng/demo-session.pi) against that dump. See the
[preview quickstart](../docs/preview-quickstart.md).

`PreviewDemoIntegrationTests` asserts the exact answer of every expression the demo session submits, so changing this
sample's state means updating those expectations in the same change.
