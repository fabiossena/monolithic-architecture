# E-commerce Monolith — Catalog, Cart, Orders, Payments, Inventory

Implementation of the **Simple Monolith Design** from the reference diagram:

```
Browser --->  API / UI Controller  (endpoints /api/* + wwwroot)
                 |
        +--------+--------+----------+------------+
        v        v        v          v            v
   Catalog    Cart     Orders    Payments    Inventory
   Service   Service   Service    Service     Service
        +--------+--------+----------+------------+
                         |
              Single Relational Database (SQLite, shared schema)
```

- **Single process / single deployable unit** — `dotnet run` or `docker compose up` starts everything.
- **A separate services layer** (`Services/`) that still runs in the same process and
  against the same schema.
- **A single relational database** — SQLite (`monolith.db`), created automatically on startup.

## Running the project

### Locally (dotnet run)

```bash
cd src/EcommerceMonolith
dotnet run
```

Open <http://localhost:5088> (the port is configured in `Properties/launchSettings.json`).

Requirements: .NET 9 SDK. Nothing else to install — SQLite comes from the EF Core package.

### With Docker Compose (recommended)

```bash
docker compose up --build        # http://localhost:5088
```

| Command | What it does |
|---|---|
| `docker compose up --build` | builds the image and starts the app on <http://localhost:5088> |
| `docker compose logs -f app` | follows the application logs |
| `docker compose ps` | shows the container and its health status |
| `docker compose down` | stops and removes the container (the database volume is kept) |
| `docker compose down -v` | stops everything **and** wipes the database volume |

Change the published port with `APP_PORT=8080 docker compose up` (Windows:
`$env:APP_PORT=8080; docker compose up`).

### With plain Docker

```bash
docker build -t ecommerce-monolith .
docker run --rm -p 5088:8080 -v monolith-data:/app/data ecommerce-monolith
```

The image alone is enough — no other service is required, because the monolith
ships its own database.

### How the container is set up

- **Multi-stage build** — `dotnet/sdk:9.0` compiles, `dotnet/aspnet:9.0` runs;
  only the published output ends up in the final image.
- **One process, one port** — the app listens on `8080` inside the container and
  is published on `5088` on the host.
- **Non-root** — the application runs as the `app` user of the base image.
- **Persistent database** — the SQLite file lives in `/app/data`, mounted as the
  named volume `monolith-data`, so the data survives restarts and image upgrades.
- **`HEALTHCHECK`** — the container reports *healthy* once `GET /health` answers;
  `docker compose ps` shows that status live.

## End-to-end features

| Flow | How to test it |
|---|---|
| **Catalog** | Text search plus category filter (`#/catalog`) |
| **Cart** | Add items, change quantity, remove items, empty the cart (side drawer). Stock is validated when adding |
| **Checkout -> Order** | Modal with customer details -> creates an order **already paid** with a transaction |
| **Payment declined** | Tick *Simulate a payment failure* -> the order comes out as **Cancelled**, the payment as **Declined** and the stock is **NOT** deducted |
| **Orders** | List with status, payment and a detail modal (`#/orders`) |
| **Inventory** | Set the quantity, +10 / -50 / restock, low-stock and out-of-stock KPIs (`#/inventory`) |

## Project structure

```
monolithic-architecture/
|-- Dockerfile                     # multi-stage build (SDK -> ASP.NET runtime)
|-- docker-compose.yml             # app + persistent volume for the SQLite database
|-- .dockerignore
|-- Monolith.sln
`-- src/EcommerceMonolith/
    |-- Program.cs                  # bootstrap of the single process (+ /health)
    |-- Api/Endpoints.cs            # API / UI Controller (every /api route lives here)
    |-- Services/                   # modules of the monolith
    |   |-- CatalogService.cs
    |   |-- CartService.cs
    |   |-- OrdersService.cs        # orchestrates checkout in ONE transaction
    |   |-- PaymentsService.cs      # simulated gateway (approve / decline)
    |   `-- InventoryService.cs     # the only writer of stock quantities
    |-- Domain/Entities.cs          # Product, Cart, Order, Payment, Inventory
    |-- Data/                       # DbContext (single schema) + seed data
    `-- wwwroot/                    # UI (index.html, styles.css, app.js)
```

## Design decisions

- **Transactional checkout**: `OrdersService.CheckoutAsync` validates the stock, charges the
  payment, creates the order and deducts the stock inside a **single** database
  transaction. If any step fails, everything is rolled back — feasibility guaranteed by the
  shared schema (the main strength of the monolith).
- **The Inventory Service is the only writer** of stock quantities; the Catalog and Cart
  services only read them.
- **Simulated payments**: a realistic `ChargeAsync -> (approved, transactionId, reason)`
  contract, with a UI button that forces a decline so the stock rollback can be demonstrated.
- **The cart state is persisted** in the database (it survives a process restart) and is
  identified by a `cartId` generated in the browser.

## Limitations (the "Key Points (Simple)" of the diagram)

1. Process simplicity — fast development, low initial complexity.
2. Harder module boundaries — the services share the `DbContext` and the entities.
3. Single point of failure — one process, one database.

> The *Enterprise* variant (stateless replicas, Redis, a message queue, master/replica
> database) would be the natural next step for this same codebase.
