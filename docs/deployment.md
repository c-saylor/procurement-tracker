# ProcureTrack — Deployment and Production Turnover

This document describes how ProcureTrack would be deployed to **Windows Server with IIS** and **SQL Server**, and what must be completed before a production release.

> **Status:** these steps describe the intended procedure. They have **not** been executed against a production server. The current build is a portfolio demonstration and **must not be deployed to production as is**. See the blockers in [Section 9](#9-production-turnover-checklist).

## 1. Architecture in production

```
Users (browser, HTTPS)
        │
        ▼
IIS (Windows Server) ── ASP.NET Core Module ── ProcureTrack (in-process)
        │
        ▼ Windows authentication (app pool identity / service account)
SQL Server ── ProcureTrack database (+ usp_ProcurementReport)
```

## 2. Server prerequisites

- Windows Server with the **IIS** role enabled
- **ASP.NET Core 10.0 Runtime – Windows Hosting Bundle**, which installs the .NET runtime and the ASP.NET Core Module that lets IIS host the app. Restart IIS after installing it (`iisreset`).
- A SQL Server instance reachable from the web server
- A TLS certificate for the site's host name

## 3. Build and publish

On a build machine, from the repository root:

```powershell
dotnet test
dotnet publish src/ProcureTrack.Web -c Release -o ./publish
```

`dotnet publish` produces the compiled app, its static files, and a generated `web.config` that tells IIS to run it through the ASP.NET Core Module (in-process hosting). Copy the contents of `./publish` to the server, for example to `D:\Sites\ProcureTrack`.

## 4. IIS configuration

1. **Application pool:** create a pool named `ProcureTrack`, with **.NET CLR version: No Managed Code** (the ASP.NET Core Module hosts .NET itself) and pipeline mode Integrated.
2. **Identity:** run the pool as a dedicated **domain service account** (preferably a group managed service account), so it can connect to SQL Server with Windows authentication and no password in configuration.
3. **Load User Profile = True** on the pool. ASP.NET Core stores the **Data Protection keys** that protect sign-in cookies and antiforgery tokens in the user profile; without them, keys are lost on every restart and users are signed out. A web farm would need a shared key store instead.
4. **Site:** point the physical path at the publish folder, add an **HTTPS binding** with the certificate, and remove any HTTP-only binding or redirect it to HTTPS. The app also sends HSTS headers in Production.

## 5. Database

Startup migrations and seeding run **only in Development**. In production, the database is changed only through reviewed scripts.

1. **Generate an idempotent script** (safe to run against a database at any migration level):

```powershell
   dotnet ef migrations script --idempotent --project src/ProcureTrack.Web --output deploy/migrate.sql
```

2. **Back up** the production database, then have a DBA review and run `migrate.sql`. It creates the tables, constraints, indexes, and `usp_ProcurementReport`.

3. **Grant least privilege** to the app's service account, never `db_owner`:

```sql
   CREATE LOGIN [DOMAIN\svc-procuretrack] FROM WINDOWS;
   USE ProcureTrack;
   CREATE USER [DOMAIN\svc-procuretrack] FOR LOGIN [DOMAIN\svc-procuretrack];
   ALTER ROLE db_datareader ADD MEMBER [DOMAIN\svc-procuretrack];
   ALTER ROLE db_datawriter ADD MEMBER [DOMAIN\svc-procuretrack];
   GRANT EXECUTE ON dbo.usp_ProcurementReport TO [DOMAIN\svc-procuretrack];
```

4. **Load reference data.** ProcureTrack has **no admin screens for departments and vendors**. A production database starts empty, so departments and vendors must be loaded by a reviewed SQL script.

## 6. Configuration

The production connection string is supplied as an **environment variable**, never committed to source control. One option is inside the generated `<aspNetCore>` element of `web.config` on the server:

```xml
<environmentVariables>
  <environmentVariable name="ConnectionStrings__ProcureTrack"
                       value="Server=SQLPROD01;Database=ProcureTrack;Trusted_Connection=True;Encrypt=True" />
</environmentVariables>
```

- Windows authentication means the connection string holds **no password**.
- Production uses `Encrypt=True` with a **trusted** SQL Server certificate. Do not carry over the development setting `TrustServerCertificate=True`.
- `ASPNETCORE_ENVIRONMENT` defaults to **Production** when unset. This enables the friendly error page and HSTS, and disables the developer exception page and startup seeding.

## 7. Authentication for production (required code change)

The demo sign-in page lets any visitor choose to be an approver. Before production, it must be replaced:

1. In IIS, enable **Windows Authentication** and disable **Anonymous Authentication** for the site.
2. In the app, replace the cookie-based demo sign-in with Windows (Negotiate) authentication, remove the demo `AccountController` and user list, and map the **Requester** and **Approver** roles to Active Directory groups.
3. Store a **stable user identifier** (for example, the user's UPN or SID) in `RequestedBy` and `ActedBy` instead of display names, and compare those values for ownership and separation of duties.

## 8. Post-deployment smoke test

1. Browse to the site over HTTPS and confirm you're signed in as your Windows account.
2. Confirm the dashboard loads with the expected figures.
3. As a requester, submit a test request; as a different approver, approve it; confirm the audit entry.
4. Request a non-existent page and confirm the friendly 404 page.
5. Check the Windows Event Log and application logs for errors.

If the app fails to start, temporarily set `stdoutLogEnabled="true"` in `web.config`, reproduce the problem, read `.\logs\stdout*`, then turn it off again.

**Rollback:** keep the previous publish folder so the site can be switched back immediately; restore the pre-deployment database backup if a migration must be reversed.

## 9. Production turnover checklist

| # | Item | Blocker? | Status |
|---|---|---|---|
| 1 | Replace demo sign-in with Windows Authentication and AD role mapping (Section 7) | **Yes** | Not started |
| 2 | Store stable user IDs instead of display names (Section 7) | **Yes** | Not started |
| 3 | Admin process or screens for departments and vendors | **Yes** | Not started |
| 4 | Code review completed and approved | Yes | — |
| 5 | `dotnet test` passes; full manual regression from the [test plan](test-plan.md) completed | Yes | — |
| 6 | `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages | Yes | — |
| 7 | Database backup taken; idempotent migration script reviewed by a DBA | Yes | — |
| 8 | Service account created with least-privilege database permissions | Yes | — |
| 9 | Connection string configured on the server; no secrets in source control | Yes | — |
| 10 | HTTPS certificate installed; HTTP redirected or removed | Yes | — |
| 11 | App pool configured (No Managed Code, Load User Profile) | Yes | — |
| 12 | Reference data loaded | Yes | — |
| 13 | Post-deployment smoke test passed | Yes | — |
| 14 | User guide distributed; initial training delivered | No | — |
| 15 | Support contact and rollback plan communicated | No | — |
| 16 | Content Security Policy and rate limiting evaluated | No | — |