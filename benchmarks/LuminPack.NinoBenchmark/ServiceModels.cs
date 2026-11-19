// Adapted from the Nino benchmark suite. See LICENSE.Nino.txt.
using LuminPack.Attribute;
using System;
using System.Collections.Generic;
using System.Linq;
using Nino.Core;

#nullable disable
// PBN0023: interfaces as polymorphic contracts are what these workloads measure (the members are on the implementations).
// PBN0013: Envelope declares each closed Envelope<TEvent> it carries, not the open generic.
namespace LuminPack.NinoBenchmark.Workloads
{

    // ---------------------------------------------------------------- RPC: a request and its response, whose result is
    // an interface with a generic implementation (Ok<T>) beside two errors.

    [NinoType, LuminPackable]
    public partial class RpcRequest
    {
        public long CallId;
        public string Service;
        public string Method;
        public Guid TraceId;
        public DateTime Deadline;
        public Dictionary<string, string> Metadata;
        public List<int> ItemIds;
    }

    [NinoType, LuminPackable]
    public partial class Item
    {
        public int Id;
        public string Name;
        public decimal Price;
        public int Stock;
    }

    /// <summary>A call's result: an interface, each kind its own type (one of them generic).</summary>
    [NinoType, LuminPackable]
    public partial interface IRpcResult { }

    [NinoType, LuminPackable]
    public partial class Ok<T> : IRpcResult
    {
        public T Value;
    }

    [NinoType, LuminPackable]
    public partial class NotFound : IRpcResult
    {
        public string Resource;
    }

    [NinoType, LuminPackable]
    public partial class FieldError
    {
        public string Field;
        public string Message;
    }

    [NinoType, LuminPackable]
    public partial class ValidationFailed : IRpcResult
    {
        public List<FieldError> Errors;
    }

    [NinoType, LuminPackable]
    public partial class RpcResponse
    {
        public long CallId;
        public DateTime ServedAt;
        public int ElapsedMicros;
        public IRpcResult Result;
    }

    /// <summary>One RPC call: the request and the response a server sends back, with 8 items.</summary>
    [NinoType, LuminPackable]
    public partial class RpcCall
    {
        public RpcRequest Request;
        public RpcResponse Response;

        /// <summary>Creates a polymorphic request for the RPC fixture.</summary>
        /// <param name="r">The seeded random number generator.</param>
        /// <returns>The generated fixture.</returns>
        public static RpcCall Create(Random r)
        {
            var ids = Enumerable.Range(0, 8).Select(_ => r.Next(1, 100_000)).ToList();
            return new RpcCall
            {
                Request = new RpcRequest
                {
                    CallId = 7_340_032_118, Service = "inventory.v1.Inventory", Method = "GetItems", TraceId = Data.NewGuid(r),
                    Deadline = Data.Epoch.AddMilliseconds(r.Next()),
                    Metadata = new Dictionary<string, string>
                        { ["authorization"] = "Bearer eyJhbGciOiJIUzI1NiJ9.e30", ["x-client"] = "web/2.31.0", ["accept-language"] = "en-US" },
                    ItemIds = ids,
                },
                Response = new RpcResponse
                {
                    CallId = 7_340_032_118, ServedAt = Data.Epoch.AddMilliseconds(r.Next()), ElapsedMicros = r.Next(100, 9_000),
                    Result = new Ok<List<Item>>
                    {
                        Value = ids.Select(id => new Item
                            { Id = id, Name = "Item " + id, Price = r.Next(99, 99_999) / 100m, Stock = r.Next(1, 500) }).ToList(),
                    },
                },
            };
        }
    }

    // ---------------------------------------------------------------- An event store's page: domain events in two levels
    // of abstract classes (three families, eight events), each in a generic envelope whose base is not generic.
    // A field typed as a family reads any of that family's subtypes.

    [NinoType, LuminPackable]
    public abstract partial class DomainEvent
    {
        public Guid EventId;
        public DateTime OccurredAt;
        public int Version;
    }

    [NinoType, LuminPackable]
    public abstract partial class OrderEvent : DomainEvent
    {
        public long OrderId;
    }

    [NinoType, LuminPackable]
    public sealed partial class OrderPlaced : OrderEvent
    {
        public Guid CustomerId;
        public decimal Amount;
        public List<int> Skus;
    }

    [NinoType, LuminPackable]
    public sealed partial class OrderShipped : OrderEvent
    {
        public string Carrier;
        public string TrackingNumber;
        public DateTime? EstimatedDelivery;
    }

    [NinoType, LuminPackable]
    public sealed partial class OrderCancelled : OrderEvent
    {
        public string Reason;
    }

    [NinoType, LuminPackable]
    public abstract partial class PaymentEvent : DomainEvent
    {
        public string PaymentId;
        public decimal Amount;
    }

    [NinoType, LuminPackable]
    public sealed partial class PaymentAuthorized : PaymentEvent
    {
        public string Currency;
        public string CardLast4;
    }

    [NinoType, LuminPackable]
    public sealed partial class PaymentCaptured : PaymentEvent
    {
        public DateTime CapturedAt;
    }

    [NinoType, LuminPackable]
    public sealed partial class PaymentRefunded : PaymentEvent
    {
        public string Reason;
        public bool Partial;
    }

    [NinoType, LuminPackable]
    public abstract partial class CustomerEvent : DomainEvent
    {
        public Guid CustomerId;
    }

    [NinoType, LuminPackable]
    public sealed partial class CustomerRegistered : CustomerEvent
    {
        public string Email;
        public string Country;
    }

    [NinoType, LuminPackable]
    public sealed partial class CustomerRenamed : CustomerEvent
    {
        public string OldName;
        public string NewName;
    }

    /// <summary>An event as the store keeps it: a non-generic base, one generic envelope per event family.</summary>
    [NinoType, LuminPackable]
    public abstract partial class Envelope
    {
        public long Sequence;
        public string Stream;
        public DateTime RecordedAt;
    }

    [NinoType, LuminPackable]
    public sealed partial class Envelope<TEvent> : Envelope where TEvent : DomainEvent
    {
        public TEvent Event;
        public Dictionary<string, string> Headers;
    }

    /// <summary>500 events of an event store's stream, in their envelopes.</summary>
    [NinoType, LuminPackable]
    public partial class EventStream
    {
        public long FromSequence;
        public List<Envelope> Events;

        /// <summary>Creates a page of polymorphic domain events.</summary>
        /// <param name="r">The seeded random number generator.</param>
        /// <returns>The generated fixture.</returns>
        public static EventStream Create(Random r) => new()
        {
            FromSequence = 5_200_000,
            Events = Enumerable.Range(0, 500).Select(i =>
            {
                var at = Data.Epoch.AddMilliseconds(i * 1_250L);
                T Base<T>(T e) where T : DomainEvent
                {
                    e.EventId = Data.NewGuid(r);
                    e.OccurredAt = at;
                    e.Version = 1 + r.Next(3);
                    return e;
                }

                Envelope envelope = (r.Next(8)) switch
                {
                    0 => new Envelope<OrderEvent> { Event = Base<OrderEvent>(new OrderPlaced
                        { OrderId = 900_000 + i, CustomerId = Data.NewGuid(r), Amount = r.Next(500, 90_000) / 100m,
                          Skus = Enumerable.Range(0, r.Next(1, 6)).Select(_ => r.Next(10_000, 99_999)).ToList() }) },
                    1 => new Envelope<OrderEvent> { Event = Base<OrderEvent>(new OrderShipped
                        { OrderId = 900_000 + i, Carrier = r.Next(2) == 0 ? "UPS" : "FedEx",
                          TrackingNumber = "1Z" + r.NextInt64().ToString("X14"),
                          EstimatedDelivery = r.Next(5) == 0 ? null : at.AddDays(r.Next(2, 9)) }) },
                    2 => new Envelope<OrderEvent> { Event = Base<OrderEvent>(new OrderCancelled
                        { OrderId = 900_000 + i, Reason = "customer request" }) },
                    3 => new Envelope<PaymentEvent> { Event = Base<PaymentEvent>(new PaymentAuthorized
                        { PaymentId = "pi_" + r.NextInt64().ToString("x16"), Amount = r.Next(500, 90_000) / 100m,
                          Currency = "USD", CardLast4 = r.Next(1_000, 9_999).ToString() }) },
                    4 => new Envelope<PaymentEvent> { Event = Base<PaymentEvent>(new PaymentCaptured
                        { PaymentId = "pi_" + r.NextInt64().ToString("x16"), Amount = r.Next(500, 90_000) / 100m, CapturedAt = at }) },
                    5 => new Envelope<PaymentEvent> { Event = Base<PaymentEvent>(new PaymentRefunded
                        { PaymentId = "pi_" + r.NextInt64().ToString("x16"), Amount = r.Next(500, 9_000) / 100m,
                          Reason = "damaged", Partial = r.Next(2) == 0 }) },
                    6 => new Envelope<CustomerEvent> { Event = Base<CustomerEvent>(new CustomerRegistered
                        { CustomerId = Data.NewGuid(r), Email = "user" + r.Next(1_000_000) + "@example.com", Country = "DE" }) },
                    _ => new Envelope<CustomerEvent> { Event = Base<CustomerEvent>(new CustomerRenamed
                        { CustomerId = Data.NewGuid(r), OldName = Data.Name(r), NewName = Data.Name(r) }) },
                };
                envelope.Sequence = 5_200_000 + i;
                envelope.Stream = "orders-" + r.Next(64);
                envelope.RecordedAt = at.AddMilliseconds(r.Next(1, 40));
                switch (envelope)
                {
                    case Envelope<OrderEvent> o: o.Headers = Headers(r); break;
                    case Envelope<PaymentEvent> p: p.Headers = Headers(r); break;
                    case Envelope<CustomerEvent> c: c.Headers = Headers(r); break;
                }

                return envelope;
            }).ToList(),
        };

        /// <summary>Builds the metadata for a generic event envelope.</summary>
        /// <param name="r">The seeded random number generator.</param>
        /// <returns>The correlation and producer headers.</returns>
        private static Dictionary<string, string> Headers(Random r) => new()
            { ["correlation-id"] = r.NextInt64().ToString("x16"), ["producer"] = "checkout-api" };
    }

    // ---------------------------------------------------------------- 1,000 structured log events a service ships to
    // its collector in one request.

    public enum Severity { Debug, Info, Warning, Error }

    [NinoType, LuminPackable]
    public partial class LogEvent
    {
        public DateTime Timestamp;
        public Severity Level;
        public string Template;
        public string TraceId;
        public double ElapsedMs;
        public Dictionary<string, string> Tags;
    }

    [NinoType, LuminPackable]
    public partial class LogBatch
    {
        public string Service;
        public string Host;
        public List<LogEvent> Events;

        private static readonly string[] Templates =
        {
            "HTTP {Method} {Path} responded {StatusCode} in {Elapsed} ms", "Cache miss for {Key}",
            "User {UserId} signed in", "Retrying {Operation} after {Delay} ms (attempt {Attempt})",
            "Order {OrderId} placed by {CustomerId}", "Payment {PaymentId} captured", "Queue {Queue} depth is {Depth}",
            "Slow query on {Table}: {Elapsed} ms", "Token refreshed for client {ClientId}", "Health check passed",
        };
        private static readonly string[] Paths = {"/api/orders", "/api/users/me", "/api/cart", "/api/search", "/health"};

        /// <summary>Creates the batch of events sent to the log service.</summary>
        /// <param name="r">The seeded random number generator.</param>
        /// <returns>The generated fixture.</returns>
        public static LogBatch Create(Random r) => new()
        {
            Service = "checkout-api",
            Host = "checkout-api-7d9f8c6b5-x2kqp",
            Events = Enumerable.Range(0, 1000).Select(i => new LogEvent
            {
                Timestamp = Data.Epoch.AddMilliseconds(i * 37 + r.Next(37)),
                Level = (Severity)(r.Next(20) switch { 0 => 3, < 3 => 2, < 8 => 0, _ => 1 }),
                Template = Templates[r.Next(Templates.Length)],
                TraceId = r.NextInt64().ToString("x16") + r.NextInt64().ToString("x16"),
                ElapsedMs = Math.Round(r.NextDouble() * 250, 3),
                Tags = new Dictionary<string, string>
                {
                    ["path"] = Paths[r.Next(Paths.Length)], ["status"] = r.Next(10) == 0 ? "500" : "200",
                    ["region"] = r.Next(2) == 0 ? "us-west-2" : "eu-central-1",
                },
            }).ToList(),
        };
    }
}
