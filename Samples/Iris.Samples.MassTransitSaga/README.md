# Iris MassTransit saga sample

A tiny MassTransit 8.5.10 app with one state machine, `OrderStateMachine`, used to exercise the Sagas page in Iris end to end.

States: Submitted, Accepted, Shipped, Cancelled, and MassTransit's own Final. Cancelling finalizes the saga, so the graph draws Final with the double border a final state gets. Messages (namespace `Iris.Samples.MassTransitSaga.Contracts`, all carrying `Guid OrderId`):

| Message | Valid in | Goes to |
| --- | --- | --- |
| SubmitOrder | Initial | Submitted |
| AcceptOrder | Submitted | Accepted |
| ShipOrder | Accepted | Shipped |
| CancelOrder | Submitted, Accepted | Cancelled, then Final |

## MassTransit version

The sample uses the MassTransit version pinned in the repository's central package versions, currently 8.5.10, and it must stay in step with the version Iris itself references. Iris reflects this assembly to find the state machine, and a type loaded from a different MassTransit assembly would not match, so the Sagas page would list nothing at all.

Do not move to MassTransit 9.x. That line is commercially licensed and refuses to build a bus without a license key, which would stop this sample from starting.

## Run

1. Start RabbitMQ locally, for example `docker run -d --name rabbit -p 5672:5672 -p 15672:15672 rabbitmq:3-management`.
2. `dotnet run --project Samples/Iris.Samples.MassTransitSaga`. To have it drive one order through Submitted, Accepted, Shipped on its own, append `-- --demo`; the `--` separator is what hands the flag to the sample rather than to the `dotnet run` command itself.

Environment variables: `OTEL_EXPORTER_OTLP_ENDPOINT` (default `http://127.0.0.1:4318`, which is the Iris receiver's default; the sample appends `/v1/traces` and exports traces only) and `RABBITMQ_HOST` (default `localhost`).

## Walk it through Iris

1. In Iris, open Packages and load `Samples/Iris.Samples.MassTransitSaga/bin/Debug/net11.0/Iris.Samples.MassTransitSaga.dll`.
2. Open Sagas. `OrderStateMachine` is listed; select it to see the graph.
3. Turn on the OTLP receiver in the page header. The chip shows Listening on `http://127.0.0.1:4318`.
4. Start the sample (step 2 above). Run it with `-- --demo` to see an instance appear and move through three states, or send messages yourself:
   - Open Messaging, connect to the local RabbitMQ (Iris auto-discovers it), pick the `order-state` queue.
   - Choose the `SubmitOrder` type from the loaded assembly, choose MassTransit as the framework, fill in an `OrderId`, send.
   - Back on Sagas, the instance appears with Submitted highlighted. Send `AcceptOrder` and `ShipOrder` with the same `OrderId` and watch it move.

The sample stays on MassTransit 8.5.10 through the repository's central package versions. Do not bump it to 9.x, for the licensing reason above.
