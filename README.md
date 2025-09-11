# MyApp (.NET 8, Single Host, DDD) — Auto-mapped AppServices + Permissions

- **Single host**: MVC (cookies) + API (JWT)
- **DDD layout**: Domain, Application, Infrastructure, Host
- **Auto endpoints**: All **public methods** on **AppService implementations** (classes implementing `IAppService`) are exposed as HTTP endpoints under `/api/{AppServiceType}/{Method}` via Minimal APIs.
- **Permissions**: `[RequiresPermission("X")]` on a method wires to `RequireAuthorization("Permission:X")` which your dynamic policy + handler enforce.
- **Swagger** tuned for external consumers (e.g., Power Pages) with Bearer support and XML comments.

## Run
1. Update `src/MyApp.Host/appsettings.json` connection string.
2. `cd src/MyApp.Host && dotnet run`
3. Sign in at `/Identity/Account/Login` using `admin@local / Pass@word1`.
4. Get token: `POST /api/auth/token` with `{ "userName":"admin@local", "password":"Pass@word1" }`.
5. Call an auto-mapped endpoint, e.g.:
   - `GET  /api/ProductAppService/GetAllAsync`
   - `POST /api/ProductAppService/CreateAsync` (body: `{ "name": "A", "price": 10 }`)

## Notes
- Endpoints require **JWT** by default on the `/api` group; MVC areas use cookies.
- Swagger UI at `/swagger` lists all auto-generated endpoints; Authorize using **Bearer** for testing.
- To customize routes or verbs, adjust the `InferVerb` function in `AppServiceEndpointMapper`.
- For production: move secrets to User Secrets / Key Vault; tune Identity options; consider CORS if Power Pages is hosted on a different domain.
