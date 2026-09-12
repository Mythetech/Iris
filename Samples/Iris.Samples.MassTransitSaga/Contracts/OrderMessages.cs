namespace Iris.Samples.MassTransitSaga.Contracts;

public record SubmitOrder(Guid OrderId);
public record AcceptOrder(Guid OrderId);
public record ShipOrder(Guid OrderId);
public record CancelOrder(Guid OrderId);
