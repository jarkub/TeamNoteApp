# TeamNoteApp

A small REST backend for capturing and sharing notes across small teams. Built with **C# / ASP.NET Core 10**, **Entity Framework Core 10**, and **SQLite**.

The assessment deliberately left the technology, data model, API, and scope open. I chose to implement a working foundation for team collaboration rather than attempt a complete collaborative editor in the available time. The distinction matters: storing shared notes is straightforward; making simultaneous edits understandable and safe is a larger design problem.

## What the service does

- Anyone can register a user. Registration creates a team owned by that user and adds the owner as its first member.
- Each user owns **one** team, but can belong to multiple teams. The owner can add or remove existing users; the owner cannot remove themselves.
- Team members can create, list, read, edit, and delete their team's notes. The original author remains recorded even after someone else edits a note.
- Each request is scoped to the authenticated user and, where applicable, the requested team. Nonmembers cannot read or change another team's notes.

This is a backend API, not a browser-based note editor.

## Run locally

**Prerequisite:** .NET 10 SDK. No separate database server is required.

From the repository root:

```powershell
cd TeamNoteApp
dotnet restore
dotnet run --launch-profile https
```

The supplied HTTPS launch profile uses `https://localhost:7231`. On a new machine, you may need to trust the local ASP.NET Core development certificate:

```powershell
dotnet dev-certs https --trust
```

In the follow-up revision, the application applies its included EF Core migrations on startup, creating the SQLite database when needed. This keeps local evaluation simple; production migrations should be an explicit deployment step. The generated database and SQLite sidecar files should not be committed.

Register a user first:

```http
POST https://localhost:7231/api/users
Content-Type: application/json

{ "name": "Alice" }
```

The response includes the new user's `id`, `teamId`, and an `apiToken`. Save the token: the raw token is returned only on registration. For later requests, use:

```http
Authorization: Bearer YOUR_API_TOKEN
```

For example, a team member can create a note with:

```http
POST https://localhost:7231/api/teams/1/notes
Authorization: Bearer YOUR_API_TOKEN
Content-Type: application/json

{ "title": "Meeting notes", "content": "Review the prototype" }
```

Use the actual team ID returned at registration, not necessarily `1`.

## Endpoints

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/health` | Database connectivity check |
| `POST` | `/api/users` | Register user and create their owned team; return token |
| `GET` | `/api/users/me` | Show the authenticated user's public details |
| `GET` | `/api/teams` | List teams the user belongs to |
| `GET` | `/api/teams/{id}/members` | List members of a team (members only) |
| `POST` | `/api/teams/{id}/members` | Add an existing user: `{ "userId": 2 }` (owner only) |
| `DELETE` | `/api/teams/{id}/members/{userId}` | Remove a member (owner only; not the owner) |
| `GET` | `/api/teams/{teamId}/notes` | List notes in a team |
| `POST` | `/api/teams/{teamId}/notes` | Create a note |
| `GET` | `/api/teams/{teamId}/notes/{noteId}` | Retrieve a note |
| `PUT` | `/api/teams/{teamId}/notes/{noteId}` | Update a note's title and content |
| `DELETE` | `/api/teams/{teamId}/notes/{noteId}` | Delete a note |

Except for registration and health, endpoints require a bearer token. Note access requires team membership; membership changes require team ownership. A request for another team's notes returns `404` rather than revealing whether the resource exists. The API uses simple integer IDs internally, and currently identifies users by non-unique display names.

## Testing

The repository includes `TeamNoteApp/TeamNoteApp.http` for manual requests and `TeamNoteApp/tests/SmokeTest.ps1` for executable API checks. Run the application first, then from the `TeamNoteApp` directory:

```powershell
powershell -File .\tests\SmokeTest.ps1
```

The smoke test creates its own users and teams and checks registration, authentication, team membership and isolation, note creation/read/update/delete, authorship preservation, and selected error responses. It does not depend on a freshly emptied database. The `.http` requests are examples, not automated assertions; replace their token placeholders before use. Neither raw tokens nor local database files should be committed.

A dedicated xUnit integration-test project would provide more comprehensive, repeatable coverage, including concurrent-write scenarios.

## Three design decisions I spent the most time considering

### 1. What should a team be, and how should users participate?

I separated **ownership**, **membership**, and **note authorship** because they answer different questions:

- Ownership identifies who can manage a team's membership.
- Membership determines who can work with the team's notes.
- Authorship records who created a note, even if another member subsequently edits it.

A user can belong to many teams through a `TeamMember` join table with a composite primary key, but owns only one. Registration creates the owned team automatically; a unique index on `Team.OwnerId` enforces the ownership rule. I considered general team creation but removed it from the API to simplify onboarding and keep the exercise centered on shared notes. The user/team/membership creation sequence uses a transaction so registration cannot partially succeed.

The tradeoff is reduced flexibility: there is no separate team-creation workflow, ownership transfer, or invitation acceptance. I started adding member removal because an owner needs a way to revoke access, and completed that endpoint as a follow-up correction. A more complete product would need to define what happens to notes and membership when a user leaves or an owner departs.

### 2. Should editing overwrite a note, or create a version?

This was the main unresolved design question. A note shared by more than two people can become difficult to follow without an edit history: who changed what, when, and relative to which earlier content? Concurrent edits make the problem sharper. The current implementation updates the note record in place, keeps `CreatedByUserId` fixed, and updates `UpdatedAtUtc`. **There is no revision history or conflict detection; effectively, the last saved edit wins.**

I considered an **immutable revision model**: each edit would produce a new version referencing the version it was based on. Versions from the same predecessor could form branches instead of silently overwriting one another. A merge could reference multiple parents, forming a directed acyclic graph of revisions. This would preserve the history of competing edits and make authorship and conflict review possible.

I did **not** implement that model in the assessment. It would require defining the active revision, choosing a merge/conflict policy, and expanding the read/write API and tests. Importantly, immutability would **preserve** concurrent changes, not automatically **resolve** conflicts. Even with immutable revisions, the service would need optimistic concurrency checks (for example, an expected version or ETag) and a deliberate response when two writers edit the same version. For a time-boxed foundation, simple mutable notes were the smaller, testable choice; for genuine multi-person editing, versioning would be my first substantive design change.

### 3. How much infrastructure and identity management is justified initially?

I used **SQLite with EF Core** because the service is small, relational data is a natural fit for users/teams/notes, and reviewers can run it without provisioning a database. EF Core provides schema migrations, foreign keys, transactions, and change tracking without requiring a custom persistence layer. **Grouped Minimal APIs** keep related endpoints readable without adding controllers, repositories, or service abstractions solely for structure.

I also chose simple **per-user bearer tokens** rather than passwords or an external identity provider. Registration generates a cryptographically random 256-bit token, returns it once, and stores its SHA-256 **hash** for lookup. Because these are high-entropy machine-generated tokens rather than passwords, SHA-256 is a reasonable choice here. This provides meaningful authorization checks within the prototype, but not a complete account system: there is no recovery, expiration, rotation, revocation, or verified identity.

One identity issue became apparent quickly: a display name is not a reliable way for people to identify another user. Names can collide, while numeric IDs are awkward to exchange. I would introduce a unique normalized username or verified email address, enforced by a database constraint, before expecting people to add teammates by identity in a real product.

SQLite is not automatically a poor fit once there are more than a few users or teams. I would keep it while deployment and write-concurrency requirements remain modest. The stronger reasons to consider PostgreSQL or another server database would be sustained concurrent writes, multiple application instances, or operational requirements—not the raw number of notes alone.

## What I would change, add, or stop with more time

**Change:** Move from overwrite-in-place to immutable note revisions with explicit version identifiers and optimistic concurrency; return a conflict instead of silently losing an edit. Replace ad hoc bearer-token handling with ASP.NET Core's authentication/authorization mechanisms and an appropriate identity provider for a deployed product. Manage database migrations during deployment rather than application startup.

**Add:** Unique human-meaningful user identifiers; member invitations/acceptance and ownership transfer; version history and conflict/merge workflows; automated xUnit integration tests (especially authorization and simultaneous edits); pagination/search; structured error responses and observability. Member removal should have clearer lifecycle semantics, such as whether a removed member's historical contributions remain visible (I would retain authorship records).

**Stop or defer:** Avoid real-time co-editing, rich-text document formats, complex permission hierarchies, microservices, and premature database migration until actual product requirements justify them. In particular, I would not invest further in last-write-wins editing as a collaboration strategy beyond this prototype.

## Current limitations

This is an assessment implementation, **not a production-ready collaboration service**. Registration is open; users have no verified email or unique username; lost tokens cannot be recovered; tokens do not expire; any member can modify or delete any note in their team; note edits have no revision history or collision protection; and note lists are not paginated. The follow-up version corrects the original member-removal route and adds automatic migration on startup. Those corrections should be distinguishable from the timed submission in the repository's history.
