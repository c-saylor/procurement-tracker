# ProcureTrack — Test Plan and Results

## 1. Purpose and scope

This plan describes how ProcureTrack is verified and records the results. It covers request management, the approval workflow, authorization, filtering, the dashboard, error handling, accessibility, database constraints, and the reporting procedure.

**Out of scope:** load and performance testing, penetration testing, cross-browser testing beyond the browser listed below, and the SSRS paginated report (not delivered; see the README).

## 2. Test strategy

| Level | Purpose | Tooling | Count |
|---|---|---|---|
| Unit | Prove every approval rule in isolation, including that a failed rule leaves the request unchanged | xUnit | 21 |
| Integration | Prove the real application enforces sign-in, roles, ownership, antiforgery, over-posting protection, and the workflow through HTTP and a real SQL Server database | xUnit + `WebApplicationFactory` | 13 |
| Manual functional | Verify user-facing behavior, validation feedback, jQuery interactions, and progressive enhancement | Browser, DevTools | 60 checks |
| Database | Verify constraints and the reporting procedure directly | SSMS | Included in the manual checks |
| Accessibility | Automated audit plus keyboard checks | Lighthouse, keyboard | Included in the manual checks |

**Approach.** Business rules are tested at the unit level, where they are cheapest to test thoroughly. Security-critical wiring is tested at the integration level, so it is re-verified on every run. Manual checks cover what automation does not reach here: visual feedback, JavaScript behavior, and browser-specific features.

**Entry criteria.** The solution builds with zero errors and zero warnings; the database migrates and seeds successfully; `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages.

**Exit criteria.** All automated tests pass; all manual checks pass or are documented as known limitations; no open defects of high severity.

## 3. Test environment

| Item | Value |
|---|---|
| OS | Windows 11 (build 26100), PowerShell 5.1 |
| .NET SDK | 10.0.402 |
| Entity Framework Core | 10.0.13 |
| Database | SQL Server Express LocalDB 17.0.1000.7 (SQL Server 2025) |
| Browser | Google Chrome 155.0.8059.85 |
| Test data | Development seed: 6 departments, 8 vendors (1 inactive), 24 requests (10 Pending, 10 Approved, 4 Rejected), 14 approval records |

Reset to the seed data before a full manual run:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef database drop --force --project src/ProcureTrack.Web
dotnet run --project src/ProcureTrack.Web
```

## 4. Automated tests

Run all automated tests:

```powershell
dotnet test --logger "console;verbosity=normal"
```

**Latest result:** 34 passed, 0 failed (Oct 22, 2026).

### 4.1 Unit tests — `tests/ProcureTrack.Tests/ApprovalWorkflowTests.cs`

| Test | Cases | Verifies |
|---|---|---|
| `Approve_PendingRequest_SetsApprovedAndRecordsAudit` | 1 | Approval sets the status and records action, approver, trimmed notes, and server timestamp |
| `Approve_WithBlankNotes_SucceedsAndStoresNullNotes` | 3 | Null, empty, and whitespace notes are stored as null |
| `Approve_AlreadyDecidedRequest_Fails` | 2 | Approved and Rejected requests cannot be approved |
| `Approve_WithoutApproverName_Fails` | 3 | An approver identity is required |
| `Approve_ByRequester_FailsForSeparationOfDuties` | 2 | Requesters cannot decide their own request (case- and whitespace-insensitive) |
| `Approve_NotesOverMaximumLength_Fails` | 1 | Notes are limited to 1,000 characters |
| `Reject_PendingRequestWithReason_SetsRejectedAndStoresTrimmedReason` | 1 | Rejection sets the status and records the reason |
| `Reject_WithoutReason_Fails` | 3 | Null, empty, and whitespace reasons are refused |
| `Reject_AlreadyDecidedRequest_Fails` | 2 | Decided requests cannot be rejected |
| `CanEditAndCanDelete_OnlyAllowPendingRequests` | 3 | Only Pending requests are editable or deletable |

Every failure case also asserts that the request's status is unchanged and that no audit record was added.

### 4.2 Integration tests — `tests/ProcureTrack.Tests/Integration/AuthorizationAndWorkflowTests.cs`

These tests run the full application in memory against an isolated SQL Server test database (`ProcureTrack_Tests` on LocalDB), which is deleted and re-created at the start of every run.

| Test | Verifies |
|---|---|
| `Factory_UsesIsolatedTestDatabase` | Guard: tests never touch the development database |
| `Anonymous_Dashboard_RedirectsToSignIn` | Deny-by-default authorization |
| `Anonymous_SignInPage_IsAvailable` | The sign-in page is reachable without signing in |
| `Requester_CannotPostDecision_IsDeniedAndNothingChanges` | The Approver role is enforced on the server for decisions |
| `Approver_CannotOpenCreatePage` | The Requester role is enforced for creating requests |
| `Requester_CannotEditSomeoneElsesRequest` | Ownership is enforced |
| `Owner_CannotEditApprovedRequest_IsRedirectedToDetails` | Workflow state is enforced for the owner |
| `Approver_Approve_RecordsDecisionWithSignedInIdentity` | End-to-end approval; the approver comes from the sign-in cookie |
| `Approver_RejectWithoutReason_ShowsErrorAndChangesNothing` | The server enforces the rejection reason |
| `Approver_CannotDecideOwnRequest_SeparationOfDuties` | Separation of duties through HTTP |
| `PostWithoutAntiforgeryToken_IsRejectedWith400` | Antiforgery enforcement |
| `Create_IgnoresOverpostedStatusRequesterAndDate` | Over-posting protection |
| `MissingRequest_Returns404` | Not-found handling |

## 5. Manual QA checklist

**Result** records the initial execution when each feature was completed. **Final** is reserved for the full regression run before release.

### 5.1 Authentication and authorization

| ID | Check | Expected | Result | Run | Final |
|---|---|---|---|---|---|
| QA-AUTH-01 | Visit `/` while signed out | Redirected to `/Account/Login?ReturnUrl=%2F` | Pass | 10/21 | ☐ |
| QA-AUTH-02 | View the sign-in page | Fully styled; demo-only notice displayed | Pass | 10/21 | ☐ |
| QA-AUTH-03 | Sign in as Dana | Dashboard; user menu shows name and Requester role | Pass | 10/21 | ☐ |
| QA-AUTH-04 | As Dana, open `/PurchaseRequests/Edit/{id}` for Marcus Bell's request | Access denied page | Pass | 10/21 | ☐ |
| QA-AUTH-05 | As Morgan, open `/PurchaseRequests/Create` | Access denied; no New Request link anywhere | Pass | 10/21 | ☐ |
| QA-AUTH-06 | Load a decision form as Morgan, switch to Dana in another tab, then submit | Access denied; request stays Pending | Pass | 10/21 | ☐ |
| QA-AUTH-07 | Sign in via `/Account/Login?ReturnUrl=https://example.com` | Lands on the dashboard, not the external site | Pass | 10/21 | ☐ |
| QA-AUTH-08 | Inspect the `ProcureTrack.Auth` cookie in DevTools | HttpOnly, Secure, SameSite=Lax | Pass | 10/21 | ☐ |
| QA-AUTH-09 | As Dana, create a request | "Requested by" shows Dana Whitfield; the form has no name field | Pass | 10/21 | ☐ |
| QA-AUTH-10 | Use Sign out in the user menu | Returned to the sign-in page; protected pages redirect again | Pass | 10/22 | ☐ |

### 5.2 Request management

| ID | Check | Expected | Result | Run | Final |
|---|---|---|---|---|---|
| QA-REQ-01 | Submit a valid new request | Details page, "PR-000xx was submitted for review", status Pending | Pass | 10/21 | ☐ |
| QA-REQ-02 | Submit the form empty | Messages under each required field, without a page reload | Pass | 10/21 | ☐ |
| QA-REQ-03 | Enter amount `0`, then `2000000` | Client-side range message | Pass | 10/21 | ☐ |
| QA-REQ-04 | Enter amount `12.345` with other fields valid | Server message: no more than two decimal places | Pass | 10/21 | ☐ |
| QA-REQ-05 | Enter a past required-by date | Server message with the earliest allowed date | Pass | 10/21 | ☐ |
| QA-REQ-06 | Edit your own pending request | "was updated"; new values shown | Pass | 10/21 | ☐ |
| QA-REQ-07 | Open `/PurchaseRequests/Edit/{id}` for an Approved request | Refused with an explanation; nothing changes | Pass | 10/21 | ☐ |
| QA-REQ-08 | Open `/PurchaseRequests/Delete/{id}` for an Approved request | Refused: part of the audit trail | Pass | 10/21 | ☐ |
| QA-REQ-09 | Delete your own pending request | Confirmation page, then "was deleted"; removed from the list | Pass | 10/21 | ☐ |
| QA-REQ-10 | Edit the same request in two tabs; save tab 1, then tab 2 | Tab 2 shows the concurrency message; tab 1's change kept | Pass | 10/21 | ☐ |
| QA-REQ-11 | Create a request titled `<script>alert('xss')</script>` | Displayed as literal text; no script runs | Pass | 10/21 | ☐ |
| QA-REQ-12 | View the source of a form page | Hidden `__RequestVerificationToken` field present | Pass | 10/21 | ☐ |

### 5.3 Approval workflow

| ID | Check | Expected | Result | Run | Final |
|---|---|---|---|---|---|
| QA-DEC-01 | Open pending requests as Morgan, and as Dana | Decision form only for Morgan, on others' pending requests | Pass | 10/21 | ☐ |
| QA-DEC-02 | Choose Approve, then Reject | Notes hidden until a choice is made; label switches between "Approval notes (optional)" and "Rejection reason (required)" | Pass | 10/22 | ☐ |
| QA-DEC-03 | Submit with no choice | "Choose Approve or Reject." | Pass | 10/22 | ☐ |
| QA-DEC-04 | Reject with a blank reason | "Enter a reason for rejecting this request."; nothing sent | Pass | 10/22 | ☐ |
| QA-DEC-05 | With JavaScript disabled, reject with no reason | Server: "A reason is required to reject a request." | Pass | 10/22 | ☐ |
| QA-DEC-06 | Approve and confirm | "was approved"; Approved badge; history entry; closed to editing | Pass | 10/22 | ☐ |
| QA-DEC-07 | Reject with a reason and confirm | "was rejected"; reason shown in the history | Pass | 10/22 | ☐ |
| QA-DEC-08 | Open the confirmation dialog, then Cancel | Nothing saved | Pass | 10/22 | ☐ |
| QA-DEC-09 | Double-click the confirm button | Exactly one approval record | Pass | 10/22 | ☐ |
| QA-DEC-10 | Decide the same request in two tabs | Second decision refused: "Only pending requests can be approved or rejected." | Pass | 10/21 | ☐ |
| QA-DEC-11 | Open Details; edit the request in another tab; then approve from the first | "was changed by someone else"; stays Pending | Pass | 10/21 | ☐ |
| QA-DEC-12 | As Priya, open her own pending request, then another's | Own: separation-of-duties note, no form. Other: form shown | Pass | 10/21 | ☐ |
| QA-DEC-13 | Delete the antiforgery token in DevTools, then submit a decision | HTTP 400 with a friendly page; nothing changes | Pass | 10/22 | ☐ |
| QA-DEC-14 | Query `ApprovalRecords` in SSMS after decisions | Rows with action, approver, notes, and UTC timestamp | Pass | 10/21 | ☐ |

### 5.4 Filtering and search

| ID | Check | Expected | Result | Run | Final |
|---|---|---|---|---|---|
| QA-FLT-01 | Change the department filter | Table updates without reload; the AJAX request is visible in Network; the URL updates | Pass | 10/22 | ☐ |
| QA-FLT-02 | Type `license` quickly | One request after typing pauses; 3 results | Pass | 10/22 | ☐ |
| QA-FLT-03 | Open a filtered URL in a new tab | Same results, rendered by the server | Pass | 10/22 | ☐ |
| QA-FLT-04 | Set an end date before the start date | Inline error; no search sent | Pass | 10/22 | ☐ |
| QA-FLT-05 | Filter, open a request, press Back | Full page with filters intact | Pass | 10/22 | ☐ |
| QA-FLT-06 | Stop the server, then change a filter | "Results could not be loaded" message | Pass | 10/22 | ☐ |
| QA-FLT-07 | Disable JavaScript and use Apply filters | Filtering works with full page reloads | Pass | 10/22 | ☐ |
| QA-FLT-08 | Seed data: Facilities + Pending; Northwind vendor; no-match filter | 3 results; 4 results; empty state with Clear filters | Pass | 10/21 | ☐ |
| QA-FLT-09 | Open any request's Details page | All fields, inactive-vendor badge where relevant, approval history | Pass | 10/21 | ☐ |

### 5.5 Dashboard

| ID | Check | Expected | Result | Run | Final |
|---|---|---|---|---|---|
| QA-DASH-01 | Compare KPI cards with `SELECT Status, COUNT(*), SUM(Amount) ... GROUP BY Status` | Figures match exactly (seed: 24 total, 10 / $64,221.00 pending, $79,022 approved, 4 rejected) | Pass | 10/21 | ☐ |
| QA-DASH-02 | Click each KPI card's link | Request list pre-filtered to that status | Pass | 10/21 | ☐ |
| QA-DASH-03 | Approve a request, then return to the dashboard | Pending and approved figures update | Pass | 10/21 | ☐ |
| QA-DASH-04 | Check Recently submitted and the department breakdown | Five newest first; departments ordered by approved spending | Pass | 10/21 | ☐ |

### 5.6 Error handling

| ID | Check | Expected | Result | Run | Final |
|---|---|---|---|---|---|
| QA-ERR-01 | Open `/PurchaseRequests/Details/99999` | Styled 404 page; response status 404 | Pass | 10/22 | ☐ |
| QA-ERR-02 | Signed out, open `/no-such-page` | Styled 404 without a sign-in redirect | Pass | 10/22 | ☐ |
| QA-ERR-03 | Submit a form with the token removed | Styled 400 page | Pass | 10/22 | ☐ |
| QA-ERR-04 | Run in Production with an invalid database name and load the dashboard | Friendly error page with a reference ID; no stack trace in the browser; full exception in the server log | Pass | 10/22 | ☐ |

### 5.7 User interface and accessibility

| ID | Check | Expected | Result | Run | Final |
|---|---|---|---|---|---|
| QA-UI-01 | Phone-width viewport | Navigation collapses; tables scroll horizontally; cards stack | Pass | 10/22 | ☐ |
| QA-UI-02 | Keyboard only: skip link, forms, radio buttons, modal, Esc | Everything reachable and operable; Esc closes the modal without saving | Pass | 10/22 | ☐ |
| QA-UI-03 | Lighthouse accessibility audit: Dashboard, Purchase Requests, Details | 100 / 100 / 100 (after fixing D-03) | Pass | 10/22 | ☐ |
| QA-UI-04 | Navigate all pages | Active nav item highlighted; no template leftovers (Privacy page, default branding) | Pass | 10/22 | ☐ |

### 5.8 Database and reporting

| ID | Check | Expected | Result | Run | Final |
|---|---|---|---|---|---|
| QA-DB-01 | In SSMS, insert a rejection with NULL notes; set an amount to -5 | Both fail with the named check constraint | Pass | 10/21 | ☐ |
| QA-DB-02 | After seeding, count requests by status and approval records | 10 / 10 / 4 by status; 14 approval records | Pass | 10/21 | ☐ |
| QA-RPT-01 | Run `usp_ProcurementReport` for all departments, for Facilities, and with an inverted date range | All rows sorted by department, status, and date; Facilities only; error 50001 | Pass | 10/22 | ☐ |

## 6. Defect log

| ID | Found | Description | Severity | Resolution | Status |
|---|---|---|---|---|---|
| D-01 | 10/21, build warnings | Transitive dependency `System.Security.Cryptography.Pkcs` 9.0.11 had a high-severity and a moderate-severity denial-of-service vulnerability (CVE-2026-84008, CVE-2026-70162) | High | Pinned the patched version 10.0.13 directly; verified with `dotnet list package --vulnerable` | Closed |
| D-02 | 10/22, integration test design | When an approver sent a decision on their own request directly to the server, the server correctly refused it, but the error was placed in a form that is hidden for own requests, so no message was shown | Low | The error is now shown as a page-level message; covered by `Approver_CannotDecideOwnRequest_SeparationOfDuties` | Closed |
| D-03 | 10/22, Lighthouse | Link color contrast fell below WCAG AA 4.5:1 on the gray page background (breadcrumb, "View all" link) | Medium | Darkened the link color via Bootstrap CSS variables; Lighthouse accessibility now 100 on all audited pages | Closed |
| D-04 | 10/22, report tooling check | Microsoft Report Builder cannot connect to SQL Server Express LocalDB 2025 (error 50: "The specified LocalDB instance does not exist"), although the app and SSMS connect normally | Medium | SSRS report deferred; reporting provided by the `usp_ProcurementReport` stored procedure. A full SQL Server instance (e.g., Developer edition) would be required for Report Builder | Open (deferred) |

## 7. Results summary

| Area | Result |
|---|---|
| Automated tests | 34 / 34 passed |
| Manual checks | 60 / 60 passed (initial execution) |
| Accessibility (Lighthouse) | 100 on the Dashboard, Purchase Requests, and Details pages |
| Open defects | D-04 (deferred, documented in the README) |
| Final regression | Pending; to be completed before release |

## 8. Final regression procedure

Before release:

1. Reset the database to the seed data (Section 3).
2. Run `dotnet build` (expect zero warnings) and `dotnet test` (expect 34 passed).
3. Run `dotnet list package --vulnerable --include-transitive` (expect no vulnerable packages).
4. Execute every manual check in Section 5 and tick the **Final** column.
5. Record any failures as new defects in Section 6 and update Section 7.