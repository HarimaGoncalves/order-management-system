# OrderManagement System — Technical Documentation

> **Version:** 1.0.0
> **Platform:** .NET 10 / ASP.NET Core Web API
> **Architecture:** Clean Architecture + Domain-Driven Design (DDD)
> **Database:** Entity Framework Core (InMemory provider — swap for SQL Server/PostgreSQL in production)

---

## Table of Contents

1. [Overview](#1-overview)
2. [Architecture](#2-architecture)
3. [Solution Structure](#3-solution-structure)
4. [Domain Layer](#4-domain-layer)
5. [Application Layer](#5-application-layer)
6. [Infrastructure Layer](#6-infrastructure-layer)
7. [WebApi Layer](#7-webapi-layer)
8. [Design Patterns](#8-design-patterns)
9. [SOLID Principles Mapping](#9-solid-principles-mapping)
10. [Dependency Flow](#10-dependency-flow)
11. [API Reference](#11-api-reference)
12. [Request Execution Flow — Step by Step](#12-request-execution-flow--step-by-step)
13. [Running the Application](#13-running-the-application)
14. [Testing Strategy](#14-testing-strategy)
15. [Extending the System](#15-extending-the-system)
16. [Production Readiness Checklist](#16-production-readiness-checklist)

---

## 1. Overview

OrderManagement is a reference implementation of a **Clean Architecture** solution built with **Domain-Driven Design** tactical patterns and strict adherence to **SOLID** principles. The domain models an order lifecycle: creation, confirmation, and cancellation.

The system deliberately keeps the scope narrow — a single bounded context with one aggregate — so the architectural patterns remain clearly visible without noise from business complexity.

### Key Technical Decisions

| Decision | Rationale |
|---|---|
| MediatR for CQRS | Decouples controllers from use-case handlers; enables pipeline behaviors (validation, logging) as cross-cutting concerns without modifying handler code. |
| FluentValidation | Declarative, testable validation rules colocated with the command they validate — not scattered across controllers or entities. |
| Result\<T\> pattern | Avoids exception-driven control flow in the application layer. Handlers return explicit success/failure — controllers map to HTTP status codes. |
| EF Core InMemory | Zero infrastructure setup for development. The `IOrderRepository` and `IUnitOfWork` abstractions mean swapping to a real database requires only changing the Infrastructure registration. |
| Record types for Value Objects | C# `record` provides structural equality, immutability, and `with` expression support out of the box — a natural fit for DDD Value Objects. |
| Static Factory Method on Aggregate | `Order.Create(...)` instead of a public constructor enforces invariants at creation time and provides a single entry point for raising the `OrderCreatedEvent`. |

---

## 2. Architecture

The solution follows **Clean Architecture** (also known as Onion Architecture / Hexagonal Architecture). The fundamental rule:

> **Dependencies point inward.** Outer layers depend on inner layers. Inner layers know nothing about outer layers.

```
┌─────────────────────────────────────────────────────┐
│                    WebApi Layer                      │
│         Controllers, Program.cs, Middleware          │
│                                                     │
│  ┌─────────────────────────────────────────────┐    │
│  │             Infrastructure Layer             │    │
│  │      EF Core, Repositories, External APIs    │    │
│  │                                              │    │
│  │  ┌─────────────────────────────────────┐     │    │
│  │  │         Application Layer           │     │    │
│  │  │   Commands, Queries, Validators,    │     │    │
│  │  │   DTOs, Pipeline Behaviors          │     │    │
│  │  │                                     │     │    │
│  │  │  ┌─────────────────────────────┐    │     │    │
│  │  │  │       Domain Layer          │    │     │    │
│  │  │  │  Entities, Value Objects,   │    │     │    │
│  │  │  │  Domain Events, Interfaces  │    │     │    │
│  │  │  └─────────────────────────────┘    │     │    │
│  │  └─────────────────────────────────────┘     │    │
│  └─────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────┘
```

### Layer Responsibilities

| Layer | Responsibility | Allowed Dependencies |
|---|---|---|
| **Domain** | Business rules, entities, value objects, domain events, repository interfaces | None (pure C#, no NuGet packages) |
| **Application** | Use cases (commands/queries), validation, DTOs, orchestration | Domain |
| **Infrastructure** | Persistence (EF Core), external service implementations | Application, Domain |
| **WebApi** | HTTP concerns, serialization, routing, DI composition root | Application, Infrastructure |

---

## 3. Solution Structure

```
OrderManagement.sln
src/
├── OrderManagement.Domain/
│   ├── Entities/
│   │   ├── Entity.cs                  # Abstract base entity
│   │   ├── Order.cs                   # Aggregate Root
│   │   ├── OrderItem.cs               # Child entity
│   │   └── OrderStatus.cs             # Status enumeration
│   ├── ValueObjects/
│   │   ├── Money.cs                   # Currency-aware monetary value
│   │   └── Address.cs                 # Shipping address
│   ├── Events/
│   │   ├── OrderCreatedEvent.cs       # Raised on order creation
│   │   └── OrderCancelledEvent.cs     # Raised on cancellation
│   ├── Exceptions/
│   │   └── DomainException.cs         # Domain-specific exception
│   └── Interfaces/
│       ├── IOrderRepository.cs        # Repository contract
│       └── IUnitOfWork.cs             # Transaction boundary
│
├── OrderManagement.Application/
│   ├── Common/
│   │   ├── Behaviors/
│   │   │   └── ValidationBehavior.cs  # MediatR pipeline
│   │   └── Interfaces/
│   │       └── IResult.cs             # Result<T> monad
│   ├── Orders/
│   │   ├── Commands/
│   │   │   ├── CreateOrder/
│   │   │   │   ├── CreateOrderCommand.cs
│   │   │   │   ├── CreateOrderCommandHandler.cs
│   │   │   │   └── CreateOrderCommandValidator.cs
│   │   │   └── CancelOrder/
│   │   │       ├── CancelOrderCommand.cs
│   │   │       └── CancelOrderCommandHandler.cs
│   │   ├── Queries/
│   │   │   ├── GetOrderById/
│   │   │   │   ├── GetOrderByIdQuery.cs
│   │   │   │   └── GetOrderByIdQueryHandler.cs
│   │   │   └── GetAllOrders/
│   │   │       ├── GetAllOrdersQuery.cs
│   │   │       └── GetAllOrdersQueryHandler.cs
│   │   └── DTOs/
│   │       ├── OrderDto.cs            # Response projection
│   │       └── OrderMapper.cs         # Entity → DTO mapping
│   └── DependencyInjection.cs         # Application service registration
│
├── OrderManagement.Infrastructure/
│   ├── Persistence/
│   │   └── AppDbContext.cs            # EF Core context + IUnitOfWork
│   ├── Repositories/
│   │   └── OrderRepository.cs         # IOrderRepository implementation
│   └── DependencyInjection.cs         # Infrastructure service registration
│
└── OrderManagement.WebApi/
    ├── Controllers/
    │   └── OrdersController.cs        # REST endpoints
    ├── Properties/
    │   └── launchSettings.json
    └── Program.cs                     # Composition root
```

---

## 4. Domain Layer

The Domain layer is the **heart of the system**. It has zero external dependencies — no NuGet packages, no framework references. This is intentional: the domain model must be portable, testable in isolation, and free from infrastructure concerns.

### 4.1 Aggregate Root — `Order`

The `Order` entity is the **Aggregate Root**. All modifications to the order and its items pass through `Order` methods to enforce invariants.

```csharp
public class Order : Entity
{
    public static Order Create(string customerName, Address shippingAddress);
    public void AddItem(string productName, int quantity, Money unitPrice);
    public void Confirm();
    public void Cancel();
}
```

**Invariants enforced:**

| Rule | Enforced By |
|---|---|
| Items can only be added to pending orders | `AddItem()` checks `Status == Pending` |
| An order cannot be confirmed with zero items | `Confirm()` checks `_items.Count > 0` |
| Shipped/delivered orders cannot be cancelled | `Cancel()` checks status is not `Shipped` or `Delivered` |
| Item quantity must be positive | `OrderItem` constructor validates |
| Money amount cannot be negative | `Money` constructor validates |

**Why a static factory method?**
`Order.Create(...)` is used instead of a public constructor to:
1. Encapsulate the initialization logic (ID generation, status assignment, timestamp).
2. Guarantee the `OrderCreatedEvent` is raised on every creation — no caller can forget this step.
3. Make the intent explicit at the call site.

### 4.2 Value Objects

Value Objects are **immutable**, compared by structural equality, and have no identity.

#### `Money`
Represents a monetary amount with currency. Operations (`Add`, `Multiply`) enforce currency consistency — you cannot add USD to EUR.

```csharp
public sealed record Money(decimal Amount, string Currency)
{
    public Money Add(Money other);       // Throws if currencies differ
    public Money Multiply(int quantity);
    public static Money Zero(string currency = "USD");
}
```

#### `Address`
Represents a shipping address. Required fields are validated in the constructor.

### 4.3 Domain Events

Domain events capture **side effects** of state changes within the aggregate. They are stored in the `Entity.DomainEvents` collection and can be dispatched after persistence (via an event dispatcher or EF Core interceptors in a production system).

| Event | Raised When |
|---|---|
| `OrderCreatedEvent` | `Order.Create()` is called |
| `OrderCancelledEvent` | `Order.Cancel()` is called |

### 4.4 Repository Interfaces

The Domain layer **defines** the interfaces; the Infrastructure layer **implements** them. This is the Dependency Inversion Principle in action.

```csharp
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken ct);
    Task AddAsync(Order order, CancellationToken ct);
    Task UpdateAsync(Order order, CancellationToken ct);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
```

The repository is scoped to the `Order` aggregate. There is no `IOrderItemRepository` — items are only accessed through the aggregate root. This preserves aggregate boundaries.

---

## 5. Application Layer

The Application layer implements **use cases** using the CQRS (Command Query Responsibility Segregation) pattern via MediatR.

### 5.1 CQRS Structure

Every use case follows this convention:

```
Feature/
├── [Feature]Command.cs       or  [Feature]Query.cs        ← Request object
├── [Feature]CommandHandler.cs or  [Feature]QueryHandler.cs ← Business logic
└── [Feature]CommandValidator.cs                            ← Validation (commands only)
```

**Commands** mutate state. **Queries** read state. They are dispatched through `ISender` (MediatR) and return `Result<T>`.

### 5.2 Commands

#### `CreateOrderCommand`
- Accepts customer details, shipping address, and a list of items.
- The handler creates the aggregate, adds items, confirms the order, persists it, and returns an `OrderDto`.

#### `CancelOrderCommand`
- Accepts an `OrderId`.
- The handler loads the aggregate, calls `Cancel()`, and persists the change.
- Returns `Result<bool>.Failure(...)` if the order is not found.

### 5.3 Queries

#### `GetOrderByIdQuery`
- Returns a single `OrderDto` or a failure result if not found.

#### `GetAllOrdersQuery`
- Returns `IReadOnlyList<OrderDto>` — all orders in the system.

### 5.4 Validation Pipeline

The `ValidationBehavior<TRequest, TResponse>` is a MediatR pipeline behavior that runs **before** the handler:

```
Request → [ValidationBehavior] → [Handler] → Response
```

1. It collects all `IValidator<TRequest>` implementations registered for the request type.
2. Runs them and collects failures.
3. If any failures exist, throws a `ValidationException` before the handler executes.
4. If none exist, passes through to the handler.

This means **handlers never need to validate input** — it's already been validated by the time they execute.

### 5.5 Result Pattern

```csharp
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }

    public static Result<T> Success(T value);
    public static Result<T> Failure(string error);
}
```

Handlers return `Result<T>` instead of throwing exceptions for expected failures (e.g., "order not found"). The controller maps:
- `IsSuccess == true` → 200/201
- `IsSuccess == false` → 400/404

Exceptions are reserved for **unexpected** failures (infrastructure errors, bugs).

### 5.6 DTOs and Mapping

DTOs are **flat projections** of domain entities designed for API responses. They expose no domain behavior and use primitive types only.

`OrderMapper.ToDto(Order)` is a static method — no AutoMapper dependency. For a project this size, explicit mapping is more maintainable and debuggable than convention-based mapping.

---

## 6. Infrastructure Layer

The Infrastructure layer provides **concrete implementations** of the abstractions defined in inner layers.

### 6.1 `AppDbContext`

Extends `DbContext` and implements `IUnitOfWork`. This is a common pattern — the DbContext already tracks changes, so `SaveChangesAsync()` commits the unit of work.

**EF Core Configuration (Fluent API):**

| Entity | Configuration |
|---|---|
| `Order` | Primary key on `Id`. `ShippingAddress` mapped as an owned type. `Status` stored as string. `Items` mapped as a one-to-many relationship via shadow foreign key `OrderId`. `TotalAmount` and `DomainEvents` are ignored (computed / transient). |
| `OrderItem` | Primary key on `Id`. `UnitPrice` mapped as an owned type (`Money`). `TotalPrice` and `DomainEvents` are ignored. |

### 6.2 `OrderRepository`

Straightforward EF Core implementation. Notable decisions:
- `GetByIdAsync` uses `.Include(o => o.Items)` to eagerly load the aggregate.
- `GetAllAsync` uses `.AsNoTracking()` since it's read-only — avoids unnecessary change tracking overhead.
- `UpdateAsync` calls `_context.Orders.Update(order)` to mark the entity as modified.

### 6.3 Dependency Registration

```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection services)
{
    services.AddDbContext<AppDbContext>(options =>
        options.UseInMemoryDatabase("OrderManagementDb"));

    services.AddScoped<IOrderRepository, OrderRepository>();
    services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

    return services;
}
```

`IUnitOfWork` resolves to the **same** `AppDbContext` instance that the repository uses (both are scoped). This ensures that `SaveChangesAsync()` commits everything the repository staged within the same HTTP request.

---

## 7. WebApi Layer

The WebApi layer is the **Composition Root** — the only place where all layers are wired together.

### 7.1 `Program.cs`

```csharp
builder.Services.AddApplication();     // MediatR, FluentValidation, Pipeline Behaviors
builder.Services.AddInfrastructure();   // EF Core, Repositories, UnitOfWork
```

Each layer registers its own services via extension methods. The WebApi does not know the concrete types — it only calls the registration methods.

### 7.2 `OrdersController`

The controller is **thin**. It has no business logic — it:
1. Receives an HTTP request.
2. Maps it to a MediatR command/query (often the request body **is** the command).
3. Sends it via `ISender`.
4. Maps the `Result<T>` to an HTTP response.

```
HTTP Request → Controller → MediatR → ValidationBehavior → Handler → Repository → Database
```

---

## 8. Design Patterns

| Pattern | Where | Purpose |
|---|---|---|
| **Aggregate Root** | `Order` | Single entry point for modifying the Order + OrderItems cluster. Protects invariants. |
| **Value Object** | `Money`, `Address` | Immutable, identity-less objects. Compared by value, not reference. |
| **Domain Event** | `OrderCreatedEvent`, `OrderCancelledEvent` | Decouple side effects from the aggregate. Enables eventual consistency, audit trails, notifications. |
| **Repository** | `IOrderRepository` / `OrderRepository` | Abstracts persistence. Domain defines the contract; infrastructure provides the implementation. |
| **Unit of Work** | `IUnitOfWork` / `AppDbContext` | Atomic persistence across multiple repository operations within a single transaction. |
| **CQRS** | Commands vs Queries | Separate models for reads and writes. Commands return `Result<T>`, queries return read-optimized DTOs. |
| **Mediator** | MediatR `ISender` | Decouples the controller from the handler. Enables pipeline behaviors. |
| **Pipeline / Chain of Responsibility** | `ValidationBehavior` | Cross-cutting concerns (validation, logging, caching) injected into the request pipeline without modifying handlers. |
| **Static Factory Method** | `Order.Create(...)` | Encapsulates construction logic and guarantees domain events are raised. |
| **Result Monad** | `Result<T>` | Explicit success/failure flow without exceptions for expected errors. |

---

## 9. SOLID Principles Mapping

### Single Responsibility Principle (SRP)

Each class has **one reason to change**:

| Class | Responsibility |
|---|---|
| `Order` | Order business rules |
| `CreateOrderCommandHandler` | "Create Order" use case orchestration |
| `CreateOrderCommandValidator` | "Create Order" input validation |
| `OrderRepository` | Order persistence via EF Core |
| `OrdersController` | HTTP request/response mapping |
| `OrderMapper` | Entity-to-DTO projection |

### Open/Closed Principle (OCP)

The system is **open for extension, closed for modification**:

- Add a new use case → create a new `Command` + `Handler` + `Validator`. No existing code changes.
- Add a new pipeline behavior (e.g., logging) → implement `IPipelineBehavior<,>` and register it. No handler changes.
- Add a new repository → implement `IOrderRepository` for a different data store. No domain or application changes.

### Liskov Substitution Principle (LSP)

- `Order` and `OrderItem` extend `Entity` and uphold its behavioral contract (ID-based equality, domain events).
- Any `IOrderRepository` implementation is substitutable — the application layer works identically with InMemory, SQL Server, or a mock.

### Interface Segregation Principle (ISP)

- `IOrderRepository` defines only the methods needed by the order use cases — not a generic `IRepository<T>` with methods no one uses.
- `IUnitOfWork` has a single method: `SaveChangesAsync`. It does not expose query or tracking APIs.

### Dependency Inversion Principle (DIP)

- The **Domain** layer defines `IOrderRepository` and `IUnitOfWork`.
- The **Infrastructure** layer provides `OrderRepository` and `AppDbContext`.
- The **Application** layer depends on the abstractions (Domain interfaces), never on the implementations (Infrastructure classes).
- Wiring happens only in the **Composition Root** (`Program.cs`).

```
Application ──depends on──▶ Domain (interfaces)
                                  ▲
Infrastructure ──implements───────┘
```

---

## 10. Dependency Flow

### Compile-Time References (`.csproj`)

```
Domain              ← no references (pure)
Application         ← Domain
Infrastructure      ← Application (transitively includes Domain)
WebApi              ← Application + Infrastructure
```

### Runtime Dependency Injection

```
OrdersController
  └─ ISender (MediatR)
       └─ CreateOrderCommandHandler
            ├─ IOrderRepository  → OrderRepository  → AppDbContext
            └─ IUnitOfWork       → AppDbContext (same scoped instance)
```

The **Composition Root** (`Program.cs`) is the only place where concrete types are mapped to abstractions. No other layer performs `new ConcreteService()`.

---

## 11. API Reference

**Base URL:** `https://localhost:7183` (HTTPS) or `http://localhost:5185` (HTTP)

### `POST /api/orders` — Create Order

**Request Body:**
```json
{
  "customerName": "John Doe",
  "street": "123 Main St",
  "city": "Springfield",
  "state": "IL",
  "zipCode": "62701",
  "country": "US",
  "items": [
    { "productName": "Laptop", "quantity": 1, "unitPrice": 999.99 },
    { "productName": "Mouse", "quantity": 2, "unitPrice": 29.99 }
  ]
}
```

**Success Response — `201 Created`:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "customerName": "John Doe",
  "status": "Confirmed",
  "totalAmount": 1059.97,
  "currency": "USD",
  "createdAt": "2026-04-01T12:00:00Z",
  "items": [
    {
      "id": "...",
      "productName": "Laptop",
      "quantity": 1,
      "unitPrice": 999.99,
      "totalPrice": 999.99
    },
    {
      "id": "...",
      "productName": "Mouse",
      "quantity": 2,
      "unitPrice": 29.99,
      "totalPrice": 59.98
    }
  ]
}
```

**Validation Error — `400 Bad Request`:**
Returned when FluentValidation fails (e.g., empty customer name, no items).

---

### `GET /api/orders/{id}` — Get Order by ID

**Success — `200 OK`:** Returns `OrderDto`.

**Not Found — `404`:**
```json
{ "error": "Order not found." }
```

---

### `GET /api/orders` — Get All Orders

**Success — `200 OK`:** Returns `OrderDto[]`.

---

### `PATCH /api/orders/{id}/cancel` — Cancel Order

**Success — `204 No Content`**

**Failure — `400 Bad Request`:**
```json
{ "error": "Cannot cancel a shipped or delivered order." }
```

---

## 12. Request Execution Flow — Step by Step

This section traces the **full execution path** of every endpoint, layer by layer, explaining **what happens** and **why** each layer is involved. Understanding this flow is critical to navigating the codebase and diagnosing issues — you should always know exactly which class is executing at any given point in the pipeline.

### 12.1 General Pipeline (applies to all endpoints)

Every HTTP request follows the same high-level pipeline before reaching endpoint-specific logic:

```
  HTTP Request
      │
      ▼
┌──────────────────────────────────────────────────────────────────────┐
│  ASP.NET Core Middleware Pipeline (WebApi Layer)                     │
│  Routing, model binding, content negotiation                        │
│  WHY: Framework responsibility — parse HTTP into C# objects.        │
│  The WebApi layer is the only layer that knows HTTP exists.         │
└──────────────────┬───────────────────────────────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────────────────────────────┐
│  OrdersController (WebApi Layer)                                     │
│  Receives the request, constructs a Command/Query, sends via ISender│
│  WHY: Thin controller. Its ONLY job is to translate between HTTP    │
│  and application-layer concepts. No business logic lives here.      │
│  This keeps HTTP concerns (status codes, headers, routing)          │
│  separated from use-case logic.                                     │
└──────────────────┬───────────────────────────────────────────────────┘
                   │  ISender.Send(command/query)
                   ▼
┌──────────────────────────────────────────────────────────────────────┐
│  MediatR Dispatcher (Application Layer)                              │
│  Resolves the correct handler from DI. Runs pipeline behaviors.     │
│  WHY: The Mediator pattern decouples the sender (controller) from   │
│  the handler. The controller doesn't know which class handles its   │
│  request — it just sends a message. This makes both sides           │
│  independently testable and replaceable.                            │
└──────────────────┬───────────────────────────────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────────────────────────────┐
│  ValidationBehavior<TRequest, TResponse> (Application Layer)         │
│  Runs all IValidator<TRequest> implementations for this request.    │
│  WHY: Cross-cutting validation as a pipeline behavior means         │
│  handlers never validate input — they receive pre-validated data.   │
│  If validation fails, a ValidationException is thrown and the       │
│  handler is NEVER reached. This enforces SRP: validators validate,  │
│  handlers orchestrate.                                              │
└──────────────────┬───────────────────────────────────────────────────┘
                   │  (only if validation passes)
                   ▼
┌──────────────────────────────────────────────────────────────────────┐
│  Command/Query Handler (Application Layer)                           │
│  Orchestrates the use case: calls domain methods, uses repository.  │
│  WHY: This is the use case. It coordinates between the Domain       │
│  (business rules) and Infrastructure (persistence) without          │
│  containing business logic itself. Think of it as a screenplay      │
│  director — it tells the actors (domain objects) what scene to      │
│  play, but doesn't act itself.                                      │
└──────────────────┬───────────────────────────────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────────────────────────────┐
│  Domain Entities / Value Objects (Domain Layer)                      │
│  Execute business rules, enforce invariants, raise domain events.   │
│  WHY: The domain model owns the rules. "Can this order be           │
│  cancelled?" is a business question answered by Order.Cancel(),     │
│  not by the handler or controller. This guarantees consistency —    │
│  no matter who calls Cancel(), the same rules apply.                │
└──────────────────┬───────────────────────────────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────────────────────────────┐
│  Repository + UnitOfWork (Infrastructure Layer)                      │
│  Persists the aggregate via EF Core. Commits the transaction.       │
│  WHY: The domain and application layers don't know how data is      │
│  stored. They call IOrderRepository.AddAsync() and                  │
│  IUnitOfWork.SaveChangesAsync() — the infrastructure decides        │
│  whether that goes to SQL Server, PostgreSQL, or an in-memory DB.   │
│  This is Dependency Inversion in action.                            │
└──────────────────┬───────────────────────────────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────────────────────────────┐
│  Controller maps Result<T> to HTTP Response (WebApi Layer)           │
│  IsSuccess → 200/201/204 | !IsSuccess → 400/404                    │
│  WHY: HTTP status codes are a WebApi concern. The handler returns   │
│  Result<T> — it doesn't know or care about HTTP. The controller     │
│  translates the result into the appropriate response.               │
└──────────────────────────────────────────────────────────────────────┘
```

Now let's trace each endpoint in detail.

---

### 12.2 `POST /api/orders` — Create Order

This is the most complex flow because it touches **all four layers** and exercises the full pipeline: validation, domain construction, persistence, and DTO projection.

```
Step  Layer            Class / Method                       What Happens
────  ───────────────  ───────────────────────────────────  ─────────────────────────────────────────────
 1    WebApi           OrdersController.Create()            ASP.NET model-binds the JSON body into a
                                                            CreateOrderCommand. The controller calls
                                                            _sender.Send(command).

 2    Application      MediatR Dispatcher                   Resolves CreateOrderCommandHandler from DI.
                                                            Before calling it, runs pipeline behaviors.

 3    Application      ValidationBehavior                   Finds CreateOrderCommandValidator in DI.
                       → CreateOrderCommandValidator        Validates: CustomerName not empty (≤200 chars),
                                                            Street/City/Country not empty, Items not empty,
                                                            each item has valid ProductName/Quantity/Price.
                                                            ✗ If invalid → throws ValidationException.
                                                              The handler is NEVER called.
                                                            ✓ If valid → passes to handler.

 4    Application      CreateOrderCommandHandler.Handle()   Begins use-case orchestration.

 5    Domain           new Address(...)                     Constructs the Address value object.
                                                            WHY here: Address validates its own invariants
                                                            (street, city, country required). The handler
                                                            doesn't repeat these checks.

 6    Domain           Order.Create(customerName, address)  Static factory creates the aggregate:
                                                            • Generates a new Guid for the Order ID.
                                                            • Sets Status = Pending.
                                                            • Sets CreatedAt = UtcNow.
                                                            • Raises OrderCreatedEvent.
                                                            WHY a factory: ensures the domain event is
                                                            always raised — no caller can skip it.

 7    Domain           order.AddItem(name, qty, unitPrice)  For each item in the command:
                       → new Money(unitPrice, "USD")        • Money validates amount ≥ 0.
                       → new OrderItem(name, qty, money)    • OrderItem validates name and qty > 0.
                                                            • Order.AddItem() checks Status == Pending.
                                                            WHY through the aggregate: only the Order
                                                            decides when items can be added. External
                                                            code cannot bypass this check.

 8    Domain           order.Confirm()                      Transitions Status to Confirmed.
                                                            • Checks Status == Pending.
                                                            • Checks _items.Count > 0.
                                                            WHY: An order with no items cannot be confirmed.
                                                            This is a domain invariant, not a validation
                                                            rule — it protects data integrity.

 9    Infrastructure   OrderRepository.AddAsync(order)      Calls _context.Orders.AddAsync(order).
                                                            EF Core begins tracking the Order and its
                                                            OrderItems for insertion.
                                                            WHY: The handler calls an abstraction
                                                            (IOrderRepository). It doesn't know this is
                                                            EF Core under the hood.

10    Infrastructure   AppDbContext.SaveChangesAsync()       Commits the tracked entities to the database
                       (via IUnitOfWork)                     in a single transaction.
                                                            WHY separate from AddAsync: the Unit of Work
                                                            pattern lets the handler stage multiple
                                                            operations before committing atomically.

11    Application      OrderMapper.ToDto(order)             Projects the Order entity into an OrderDto.
                                                            WHY: We never expose domain entities over the
                                                            wire. DTOs are flat, serialization-friendly,
                                                            and decouple the API contract from the domain
                                                            model. The domain can evolve without breaking
                                                            API consumers.

12    Application      return Result<OrderDto>.Success(dto) Wraps the DTO in a success result.

13    WebApi           Controller receives Result            Maps to CreatedAtAction (HTTP 201) with the
                                                            DTO as the response body and a Location header
                                                            pointing to GET /api/orders/{id}.
```

**Layers accessed:** WebApi → Application → Domain → Infrastructure → Application → WebApi

**Why every layer was needed:**
- **WebApi**: Translate HTTP to command and result back to HTTP.
- **Application**: Orchestrate the use case; validate input; map output.
- **Domain**: Construct the aggregate with business rules; enforce invariants.
- **Infrastructure**: Persist the aggregate to the database.

---

### 12.3 `GET /api/orders/{id}` — Get Order by ID

This is a **read-only** query. It's simpler because it doesn't modify domain state — but the layer separation still matters.

```
Step  Layer            Class / Method                       What Happens
────  ───────────────  ───────────────────────────────────  ─────────────────────────────────────────────
 1    WebApi           OrdersController.GetById(id)         Extracts the Guid from the route. Sends
                                                            GetOrderByIdQuery(id) via MediatR.

 2    Application      MediatR Dispatcher                   Resolves GetOrderByIdQueryHandler.
                       → ValidationBehavior                 No IValidator<GetOrderByIdQuery> is registered,
                                                            so the behavior passes through immediately.
                                                            WHY no validator: The route constraint
                                                            {id:guid} already ensures the ID is a valid
                                                            Guid. There's nothing else to validate.

 3    Application      GetOrderByIdQueryHandler.Handle()    Calls _orderRepository.GetByIdAsync(id).

 4    Infrastructure   OrderRepository.GetByIdAsync(id)     Executes:
                                                            _context.Orders
                                                              .Include(o => o.Items)
                                                              .FirstOrDefaultAsync(o => o.Id == id)
                                                            WHY Include: The Order aggregate includes
                                                            OrderItems. Without eager loading, Items
                                                            would be an empty collection.

 5    Application      Handler checks result                If null → returns Result.Failure("Not found")
                                                            If found → maps to DTO via OrderMapper.ToDto()
                                                            WHY Result instead of exception: "Not found"
                                                            is an expected outcome, not an error. Using
                                                            Result keeps the flow explicit and avoids
                                                            the overhead and ambiguity of exceptions.

 6    WebApi           Controller maps result               IsSuccess → Ok(dto) with HTTP 200.
                                                            !IsSuccess → NotFound({ error }) with HTTP 404.
```

**Layers accessed:** WebApi → Application → Infrastructure → Application → WebApi

**Why the Domain layer is NOT directly accessed:**
The handler calls the repository (Infrastructure, via abstraction) and receives an `Order` entity (Domain) in return. But it doesn't call any domain behavior — it only reads data and maps it. The Domain layer is accessed **indirectly** (the mapper reads `order.TotalAmount`, which invokes the `CalculateTotal()` method inside the entity), but no state mutation occurs. This is characteristic of queries in CQRS — they observe the domain model but don't drive behavior.

---

### 12.4 `GET /api/orders` — Get All Orders

Structurally identical to Get by ID, but returns a collection.

```
Step  Layer            Class / Method                       What Happens
────  ───────────────  ───────────────────────────────────  ─────────────────────────────────────────────
 1    WebApi           OrdersController.GetAll()            Sends GetAllOrdersQuery() via MediatR.

 2    Application      MediatR Dispatcher                   Resolves GetAllOrdersQueryHandler.
                       → ValidationBehavior                 Passes through (no validators registered).

 3    Application      GetAllOrdersQueryHandler.Handle()    Calls _orderRepository.GetAllAsync().

 4    Infrastructure   OrderRepository.GetAllAsync()        Executes:
                                                            _context.Orders
                                                              .Include(o => o.Items)
                                                              .AsNoTracking()
                                                              .ToListAsync()
                                                            WHY AsNoTracking: This is a read-only query.
                                                            Disabling change tracking avoids allocating
                                                            snapshot copies of every entity, reducing
                                                            memory and CPU overhead — especially important
                                                            when returning collections.

 5    Application      Handler maps all entities            orders.Select(OrderMapper.ToDto).ToList()
                                                            Returns Result<IReadOnlyList<OrderDto>>.

 6    WebApi           Controller returns Ok(result.Value)  Always HTTP 200 — an empty list is still a
                                                            valid response, not an error.
```

**Layers accessed:** WebApi → Application → Infrastructure → Application → WebApi

**Design note on performance:** In a production system with large datasets, this endpoint should accept pagination parameters (`page`, `pageSize`). The query would be passed to the repository, which would translate to `Skip/Take` in the EF Core query. The Application layer defines the contract; the Infrastructure layer optimizes the execution.

---

### 12.5 `PATCH /api/orders/{id}/cancel` — Cancel Order

This is a **write** operation that exercises the domain's invariant enforcement. It's the best example of why business rules live in the Domain layer.

```
Step  Layer            Class / Method                       What Happens
────  ───────────────  ───────────────────────────────────  ─────────────────────────────────────────────
 1    WebApi           OrdersController.Cancel(id)          Extracts Guid from route. Sends
                                                            CancelOrderCommand(id) via MediatR.
                                                            WHY PATCH: The operation partially modifies
                                                            the resource (changes status only). PATCH is
                                                            semantically correct over PUT (full replace)
                                                            or DELETE (resource removal).

 2    Application      MediatR Dispatcher                   Resolves CancelOrderCommandHandler.
                       → ValidationBehavior                 No validator registered for CancelOrderCommand.
                                                            WHY: The only input is a Guid (route-validated).
                                                            The real validation — "can this order be
                                                            cancelled?" — is a DOMAIN rule, not an INPUT
                                                            rule. FluentValidation handles input shape;
                                                            the domain handles business invariants.

 3    Application      CancelOrderCommandHandler.Handle()   Calls _orderRepository.GetByIdAsync(id).

 4    Infrastructure   OrderRepository.GetByIdAsync(id)     Loads the Order aggregate with Items.
                                                            EF Core tracks the entity for changes.
                                                            WHY tracked (no AsNoTracking): We intend to
                                                            modify the entity. EF Core needs to detect
                                                            the state change to generate the UPDATE.

 5    Application      Handler checks if order exists       If null → returns Result.Failure("Not found").
                                                            No exception, no domain call — we short-circuit
                                                            at the application layer.

 6    Domain           order.Cancel()                       THIS IS WHERE THE BUSINESS RULES EXECUTE:
                                                            • If Status is Shipped or Delivered →
                                                              throws DomainException("Cannot cancel a
                                                              shipped or delivered order.")
                                                            • Otherwise → sets Status = Cancelled.
                                                            • Raises OrderCancelledEvent.
                                                            WHY in the domain: This rule is the same
                                                            regardless of who calls Cancel() — an API
                                                            controller, a background job, a test, or a
                                                            future gRPC service. Placing it in the entity
                                                            makes the rule inescapable.

 7    Infrastructure   OrderRepository.UpdateAsync(order)   Calls _context.Orders.Update(order).
                                                            Marks the entity as Modified in the change
                                                            tracker.

 8    Infrastructure   AppDbContext.SaveChangesAsync()       Persists the status change to the database.
                       (via IUnitOfWork)

 9    Application      return Result<bool>.Success(true)    Signals success to the controller.

10    WebApi           Controller returns NoContent()       HTTP 204 — the operation succeeded but there's
                                                            no response body to return.
                                                            WHY 204 over 200: Cancel is an action, not a
                                                            query. The client doesn't need the updated
                                                            order back — it can GET it if needed.
```

**Layers accessed:** WebApi → Application → Infrastructure → Domain → Infrastructure → Application → WebApi

**Why the Domain layer is critical here:**
Without the Domain layer enforcing cancellation rules, you'd have one of two problems:
1. **Rules in the handler** — if a second handler (e.g., a background job) also cancels orders, the rule must be duplicated. Duplication leads to divergence.
2. **Rules in the controller** — even worse, the rule is tied to HTTP. A gRPC or message-queue consumer would need to reimplement it.

By placing the rule in `Order.Cancel()`, it's **physically impossible** to cancel a shipped order through any code path in the system.

---

### 12.6 Flow Comparison Matrix

| Aspect | POST Create | GET by ID | GET All | PATCH Cancel |
|---|---|---|---|---|
| **Validation** | FluentValidation (6 rules) | Route constraint only | None needed | Domain invariant |
| **Domain construction** | `Order.Create()`, `AddItem()`, `Confirm()` | Read-only (no mutation) | Read-only | `Order.Cancel()` |
| **Domain events raised** | `OrderCreatedEvent` | None | None | `OrderCancelledEvent` |
| **Repository method** | `AddAsync` | `GetByIdAsync` | `GetAllAsync` | `GetByIdAsync` + `UpdateAsync` |
| **UnitOfWork** | `SaveChangesAsync` | Not called | Not called | `SaveChangesAsync` |
| **EF Tracking** | Tracked (new entity) | Tracked (by default) | **NoTracking** | Tracked (update) |
| **Result on missing** | N/A (creating new) | `Failure("Not found")` | Empty list (valid) | `Failure("Not found")` |
| **HTTP response** | 201 + Location header | 200 + body | 200 + body | 204 (no body) |

### 12.7 Why This Flow Matters

The layered execution flow is not ceremony for its own sake. Each boundary exists to solve a specific problem:

| Boundary | Problem It Solves |
|---|---|
| Controller → MediatR | **Decoupling.** The controller doesn't know which class handles the request. You can replace, decorate, or intercept handlers without touching controller code. |
| ValidationBehavior → Handler | **Separation of concerns.** Input validation is a distinct concern from business orchestration. The handler trusts its input is already valid — simplifying its code and tests. |
| Handler → Domain | **Business rule centralization.** The handler says "what to do" (create an order), the domain decides "how and whether" (enforce invariants, raise events). Rules are reusable across all entry points. |
| Handler → Repository (via interface) | **Infrastructure independence.** The handler works with `IOrderRepository` — it compiles and runs identically whether the repository uses EF Core, Dapper, a REST client, or an in-memory dictionary. |
| Repository → Database | **Persistence encapsulation.** Query optimization, eager loading strategy, and tracking behavior are infrastructure decisions invisible to the application layer. |

The guiding principle: **each layer answers a different question.**

| Layer | Question It Answers |
|---|---|
| WebApi | "How do I expose this over HTTP?" |
| Application | "What are the steps of this use case?" |
| Domain | "What are the business rules?" |
| Infrastructure | "How do I read/write data?" |

If you find yourself writing an `if` statement, ask which question it answers — that tells you which layer it belongs in.

---

## 13. Running the Application

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Run

```bash
cd src/OrderManagement.WebApi
dotnet run
```

The API starts on `https://localhost:7183` and `http://localhost:5185`.

### Postman

Import `OrderManagement.postman_collection.json` from the repository root. The collection includes all endpoints with pre-configured test scripts that auto-propagate the `orderId` across requests.

### OpenAPI / Swagger

In development mode, the OpenAPI spec is available at:
```
GET /openapi/v1.json
```

---

## 14. Testing Strategy

This architecture is designed for **testability at every layer**.

### Unit Tests — Domain Layer

Test aggregate behavior in pure C# with no mocks and no infrastructure:

```csharp
[Fact]
public void Cancel_ShippedOrder_ThrowsDomainException()
{
    var order = Order.Create("Jane", new Address("St", "City", "ST", "00000", "US"));
    order.AddItem("Widget", 1, new Money(10, "USD"));
    order.Confirm();
    // Simulate shipping (would need a Ship() method in production)

    Assert.Throws<DomainException>(() => order.Cancel());
}
```

### Unit Tests — Application Layer

Mock `IOrderRepository` and `IUnitOfWork` to test handlers in isolation:

```csharp
[Fact]
public async Task CreateOrder_ReturnsSuccess_WhenValid()
{
    var repo = Substitute.For<IOrderRepository>();
    var uow = Substitute.For<IUnitOfWork>();
    var handler = new CreateOrderCommandHandler(repo, uow);

    var result = await handler.Handle(validCommand, CancellationToken.None);

    Assert.True(result.IsSuccess);
    await repo.Received(1).AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
}
```

### Integration Tests — Infrastructure Layer

Use the EF Core InMemory provider (or a test container with a real database) to verify repository and DbContext behavior:

```csharp
[Fact]
public async Task OrderRepository_PersistsAndRetrievesOrder()
{
    using var context = CreateInMemoryContext();
    var repo = new OrderRepository(context);

    var order = Order.Create("Test", someAddress);
    await repo.AddAsync(order);
    await context.SaveChangesAsync();

    var retrieved = await repo.GetByIdAsync(order.Id);
    Assert.NotNull(retrieved);
}
```

### End-to-End Tests — WebApi

Use `WebApplicationFactory<Program>` to spin up the full pipeline:

```csharp
[Fact]
public async Task POST_Orders_Returns201()
{
    var client = _factory.CreateClient();
    var response = await client.PostAsJsonAsync("/api/orders", createOrderPayload);
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
}
```

---

## 15. Extending the System

### Adding a New Use Case (e.g., "Ship Order")

1. **Domain:** Add `Order.Ship()` method with invariant checks. Add `OrderShippedEvent`.
2. **Application:** Create `ShipOrderCommand`, `ShipOrderCommandHandler`, and optionally a validator.
3. **WebApi:** Add `[HttpPatch("{id}/ship")]` endpoint in `OrdersController`.
4. **No existing code modified** — pure extension (Open/Closed Principle).

### Adding a New Bounded Context (e.g., "Inventory")

1. Create `OrderManagement.Inventory.Domain` and `OrderManagement.Inventory.Application` projects.
2. Communicate between contexts via **Domain Events** or an **Integration Events** bus — never direct entity references.

### Swapping the Database

1. Replace `Microsoft.EntityFrameworkCore.InMemory` with `Npgsql.EntityFrameworkCore.PostgreSQL` (or any provider).
2. Update `DependencyInjection.cs` in Infrastructure:
   ```csharp
   options.UseNpgsql(connectionString);
   ```
3. Add EF Core migrations. No other layer changes.

### Adding Cross-Cutting Concerns

Implement `IPipelineBehavior<TRequest, TResponse>`:
- **Logging:** Log request/response details.
- **Performance:** Measure and alert on slow handlers.
- **Caching:** Cache query results.
- **Authorization:** Check permissions before handler execution.

Register in `DependencyInjection.cs` — no handler modifications needed.

---

## 16. Production Readiness Checklist

Before deploying this to production, address the following:

| Area | Action |
|---|---|
| **Database** | Replace InMemory with a real provider (SQL Server, PostgreSQL). Add migrations. |
| **Domain Event Dispatching** | Implement a dispatcher (EF Core `SaveChangesInterceptor` or MediatR `INotificationHandler`) to publish domain events after persistence. |
| **Global Exception Handling** | Add middleware to catch `ValidationException`, `DomainException`, and unhandled exceptions, returning structured error responses. |
| **Logging** | Add a logging pipeline behavior. Configure structured logging (Serilog). |
| **Authentication / Authorization** | Add JWT or OAuth2 middleware. Protect endpoints. |
| **Health Checks** | Add `/health` endpoint for infrastructure probes. |
| **Rate Limiting** | Configure ASP.NET Core rate limiting middleware. |
| **Idempotency** | Add idempotency keys to `POST` endpoints to prevent duplicate order creation. |
| **Outbox Pattern** | For reliable domain event publishing, persist events alongside the aggregate in the same transaction, then dispatch asynchronously. |
| **Containerization** | Add `Dockerfile` and `docker-compose.yml`. |
| **CI/CD** | Add build, test, and deploy pipelines. |

---

*This document describes the system as of v1.0.0. Update it as the architecture evolves.*
