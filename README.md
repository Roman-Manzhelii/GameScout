# GameScout

A minimal game discovery app built with **ASP.NET Core Blazor** (.NET 8). It searches games via **RAWG**, shows details and screenshots, surfaces **best deals** via **CheapShark**, and lets you maintain a local **Backlog** using browser storage.
<img width="1899" height="972" alt="image" src="https://github.com/user-attachments/assets/fde0ada5-cb8f-45b2-815a-66e406951ff0" />
<img width="1902" height="971" alt="image" src="https://github.com/user-attachments/assets/1177ef83-6b61-4e21-ad53-f5b268545b0a" />
<img width="1900" height="968" alt="image" src="https://github.com/user-attachments/assets/1d2acc0b-1b85-4770-ba13-f1b0bd8cdc5e" />

<img width="1897" height="971" alt="image" src="https://github.com/user-attachments/assets/c9f8b133-f2b7-4c82-8940-8610c2e99067" />
<img width="1896" height="969" alt="image" src="https://github.com/user-attachments/assets/cc9adac7-b6fc-4f95-9b91-e17d3eae6b3f" />
<img width="1899" height="969" alt="image" src="https://github.com/user-attachments/assets/331b98a4-9b94-4bb5-87d2-a6f5f63dec27" />

<img width="1902" height="972" alt="image" src="https://github.com/user-attachments/assets/f1114763-ddb7-44a2-b744-a5e0ef3a95f9" />
<img width="1896" height="969" alt="image" src="https://github.com/user-attachments/assets/3c4725e6-71be-4bb7-8935-e5e241db9415" />

<img width="1909" height="975" alt="image" src="https://github.com/user-attachments/assets/da1fa941-ac96-44aa-a83b-6bcd02991b30" />

<img width="636" height="851" alt="image" src="https://github.com/user-attachments/assets/ef49a5b4-f006-4ac5-8b61-5edaa241caff" />
<img width="465" height="834" alt="image" src="https://github.com/user-attachments/assets/15ecb0ad-9972-4230-bfd4-cc04d56a31db" />




---

## Features

* **Search** games with query and sorting.
* **Details** page with Metacritic, platforms, genres, description, screenshots.
* **Deals** page with savings filter and pagination; deep links to stores.
* **Backlog** stored in browser `localStorage` with image backfill.
* **Accessibility** uses FocusOnNavigate for focus management.
* **Resilience** error boundary + graceful navigation on network failures.

---

## Tech stack

* **Blazor (Interactive Server)** on .NET 8
* **HttpClient** with typed services
* **RAWG API** for catalog data
* **CheapShark API** for price deals
* **Browser `localStorage`** via JS interop

---

## Architecture overview

```
Components
  ├─ Pages
  │   ├─ Home.razor          → search, paging
  │   ├─ GameDetails.razor   → details + deals for a game
  │   ├─ Deal.razor          → top deals listing
  │   └─ Backlog.razor       → saved games
  │
  ├─ SearchBar.razor
  ├─ GameCard.razor
  ├─ DealCard.razor
  ├─ Pager.razor
  ├─ Routes.razor            → Router + FocusOnNavigate
  └─ AppErrorBoundary.cs     → error handling

State & Services
  ├─ AppState.cs             → persisted backlog, last search
  ├─ LocalStorage.cs         → JS interop wrapper
  ├─ RawgService.cs          → RAWG endpoints
  └─ CheapSharkService.cs    → deals/stores endpoints
```

**Data flow**

1. UI triggers a service call (RAWG / CheapShark).
2. Service returns DTOs → mapped to domain models.
3. `AppState` stores search results and backlog; persisted via `LocalStorage`.
4. Network errors are caught and redirect to `/404` through `NavigationManager`.

---

## Getting started

### Prerequisites

* .NET 8 SDK
* RAWG API key (free): [https://api.rawg.io/docs](https://api.rawg.io/docs)

### Configuration

Set the RAWG key as an environment variable or in a `.env` file at the repo root:

```ini
RAWG_API_KEY=your_rawg_key_here
```

> The app reads configuration from environment (or `.env` if enabled) and passes `?key=...` to RAWG requests.

## Key implementation notes

* **JS interop timing**: calls to `localStorage` occur in `OnAfterRenderAsync` to avoid prerender issues.
* **Absolute URLs**: outbound HTTP requests use absolute endpoints to avoid `BaseAddress` errors.
* **Pagination**: windowed pager with Prev/Next and anchor return to `#page-heading`.
* **Deals parsing**: `DealCard` extracts `"Game Title | Store"` for clean display.

---

## Known behaviors

* If the network is unavailable, pages navigate to `/404`.
* Backlog images may be backfilled from RAWG on first render.

---

## References

### Official docs

1. RAWG Video Games Database API — [https://api.rawg.io/docs](https://api.rawg.io/docs)
2. CheapShark API — [https://apidocs.cheapshark.com](https://apidocs.cheapshark.com)
3. ASP.NET Core Blazor routing and navigation — [https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/routing](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/routing)
4. `FocusOnNavigate` — [https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.routing.focusonnavigate](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.routing.focusonnavigate)
5. Handle errors in Blazor — [https://learn.microsoft.com/aspnet/core/blazor/fundamentals/handle-errors](https://learn.microsoft.com/aspnet/core/blazor/fundamentals/handle-errors)
6. Blazor JavaScript interop — [https://learn.microsoft.com/aspnet/core/blazor/javascript-interoperability](https://learn.microsoft.com/aspnet/core/blazor/javascript-interoperability)
7. IHttpClientFactory in ASP.NET Core — [https://learn.microsoft.com/aspnet/core/fundamentals/http-requests](https://learn.microsoft.com/aspnet/core/fundamentals/http-requests)
8. DotNetEnv (.env loader) — [https://github.com/tonerdo/dotnet-env](https://github.com/tonerdo/dotnet-env)
9. Blazored.LocalStorage — [https://github.com/Blazored/LocalStorage](https://github.com/Blazored/LocalStorage)



### Stack Overflow Q&A consulted

* JS interop error during `OnInitializedAsync` — [https://stackoverflow.com/questions/61438796](https://stackoverflow.com/questions/61438796)
* Trigger search on Enter key — [https://stackoverflow.com/questions/63861068](https://stackoverflow.com/questions/63861068)
* `Invalid request URI` / missing BaseAddress — [https://stackoverflow.com/questions/77447224](https://stackoverflow.com/questions/77447224)
* Component change event not firing — [https://stackoverflow.com/questions/78063536](https://stackoverflow.com/questions/78063536)
* Trigger `EventCallback` on value change — [https://stackoverflow.com/questions/79116520/79207736](https://stackoverflow.com/questions/79116520/79207736)
* `localStorage` on .NET 8, IJSRuntime pitfalls — [https://stackoverflow.com/questions/78750368](https://stackoverflow.com/questions/78750368)
* `StateHasChanged` vs `OnAfterRenderAsync` — [https://stackoverflow.com/questions/76590672](https://stackoverflow.com/questions/76590672)
* `FocusOnNavigate` and `tabindex="-1"` — [https://stackoverflow.com/questions/72734337](https://stackoverflow.com/questions/72734337)
* JS `alert/confirm/prompt` via IJSRuntime — [https://stackoverflow.com/questions/60773229](https://stackoverflow.com/questions/60773229)

---

## File‑to‑reference map

| Area            | Files                                                          | Primary references                         |
| --------------- | -------------------------------------------------------------- | ------------------------------------------ |
| Routing & focus | `Routes.razor`                                                 | Blazor routing, `FocusOnNavigate`          |
| Error handling  | `AppErrorBoundary.cs`, page try/catch → `/404`                 | Error boundaries                           |
| Search & paging | `Home.razor`, `Pager.razor`, `SearchBar.razor`                 | Routing, EventCallback, Enter key handling |
| Details & deals | `GameDetails.razor`, `Deal.razor`, `DealCard.razor`            | RAWG, CheapShark APIs                      |
| Cards & backlog | `GameCard.razor`, `Backlog.razor`, `AppState.cs`               | Lifecycle docs, JS interop                 |
| HTTP            | `RawgService.cs`, `CheapSharkService.cs`, `BaseHttpService.cs` | IHttpClientFactory, API docs               |
| Storage         | `LocalStorage.cs`                                              | JS interop + Web Storage                   |

---
