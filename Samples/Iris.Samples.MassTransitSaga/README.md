# Iris MassTransit saga sample

A tiny MassTransit 9.1.0 app with one state machine, `OrderStateMachine`, used to exercise the Sagas page in Iris end to end.

States: Submitted, Accepted, Shipped, Cancelled. Messages (namespace `Iris.Samples.MassTransitSaga.Contracts`, all carrying `Guid OrderId`):

| Message | Valid in | Goes to |
| --- | --- | --- |
| SubmitOrder | Initial | Submitted |
| AcceptOrder | Submitted | Accepted |
| ShipOrder | Accepted | Shipped |
| CancelOrder | Submitted, Accepted | Cancelled |

## License

MassTransit 9.1.0 requires a license before it will build a bus, so the sample will not start without one. This is a MassTransit requirement, not an Iris one.

Supply it through the `MT_LICENSE` or `MT_LICENSE_PATH` environment variables, or in code with `SetLicense` or `SetLicenseLocation`. Get a license at https://masstransit.massient.com/configuration/license.

Iris itself needs no license: it uses MassTransit only to wrap message envelopes and never builds a bus.

## Run

1. Start RabbitMQ locally, for example `docker run -d --name rabbit -p 5672:5672 -p 15672:15672 rabbitmq:3-management`.
2. `dotnet run --project Samples/Iris.Samples.MassTransitSaga` (add `--demo` to have it drive one order through Submitted, Accepted, Shipped on its own).

Environment variables: `OTEL_EXPORTER_OTLP_ENDPOINT` (default `http://127.0.0.1:4318`, which is the Iris receiver's default) and `RABBITMQ_HOST` (default `localhost`).

## Walk it through Iris

1. In Iris, open Packages and load `Samples/Iris.Samples.MassTransitSaga/bin/Debug/net11.0/Iris.Samples.MassTransitSaga.dll`.
2. Open Sagas. `OrderStateMachine` is listed; select it to see the graph.
3. Turn on the OTLP receiver in the page header. The chip shows Listening on `http://127.0.0.1:4318`.
4. Start the sample (step 2 above). Run it with `--demo` to see an instance appear and move through three states, or send messages yourself:
   - Open Messaging, connect to the local RabbitMQ (Iris auto-discovers it), pick the `order-state` queue.
   - Choose the `SubmitOrder` type from the loaded assembly, choose MassTransit as the framework, fill in an `OrderId`, send.
   - Back on Sagas, the instance appears with Submitted highlighted. Send `AcceptOrder` and `ShipOrder` with the same `OrderId` and watch it move.

The sample stays on MassTransit 9.1.0 through the repository's central package versions. Do not bump it.
