# TrSetup Coding Standards

**Last Updated:** 2026-07-05
**Status:** Authoritative for all code under `src/`/`source/` and `tests/`. Conformance enforced via repo-root `.editorconfig` + verifier grep checks in §"Enforcement".

## Database Naming Conventions

### Tables and Columns
- PascalCase: `CustomerOrder` NOT `customer_order`
- Singular: `CustomerOrder` NOT `CustomerOrders`
- **NEVER use underscores** in any DB object name
- FK columns: `{TableName}Id` (e.g., `CustomerId`)
- PK: `{TableName}Id` (e.g., `UserId`)

### Stored Procedures & Functions
- PascalCase verb prefix: `GetCustomerOrders`, `InsertOrder`, `CalculateTotal`
- Action prefixes: Get / Insert / Update / Delete / Calculate

### Indexes & Constraints
- Index: `IX{Table}{Column}` · PK: `Pk{Table}` · FK: `Fk{Table}{Ref}` · Unique: `Uc{Table}{Column}`

## C# Conventions

### Classes & Interfaces
- PascalCase for classes; `I` prefix for interfaces; descriptive names.
- Async methods end with `Async`.

### Fields, Parameters, Locals

**NEVER use underscores** anywhere in any identifier.

| Kind | Convention | Example |
|------|-----------|---------|
| **Instance fields** | `obj` prefix + PascalCase tail (no underscores) | `private readonly ILogger<X> objLogger;`<br>`private readonly HttpClient objHttpClient;`<br>`private string objCachedPublicKey;` |
| **Static / `const` fields** | PascalCase, no prefix | `private const string CachePrefix = "…";` |
| **Method parameters** | `a` prefix + PascalCase | `LoginAsync(string aEmail, string aPassword)` |
| **Local variables** | `v` prefix + PascalCase | `var vResponse = await …` |
| **Booleans** | same prefix + `Is`/`Has`/`Can` | `IsAuthenticated`, `vIsValid`, `aHasAccess` |
| **Properties** | PascalCase, no prefix | `public string ConnectionString { get; set; }` |
| **Constants** | PascalCase, no underscores | `MaxRetryCount` NOT `MAX_RETRY_COUNT` |
| **Test methods** | Short PascalCase, no underscores — full scenario in XML `<summary>` | `LoginRejectsBadPassword` not `Login_BadPassword_ReturnsUnauthorized` |

**Rejected forms:** `_underscore` field prefixes, snake_case anywhere, Hungarian prefixes (`strName`), underscores in test method names.

> **Per-project decision (day-1, 2026-07-05):** TrSetup uses the **`obj` prefix** on instance fields (greenfield default — no existing code to detect from, no custom instruction overriding). Recorded here and in CLAUDE.md.

### Controller-action parameters
The `a`-prefix applies uniformly to `[FromRoute]`/`[FromQuery]`/`[FromBody]`. Parameter name flows through to OpenAPI. Body DTO **property** names stay PascalCase no prefix; only the parameter symbol holding the deserialized DTO gets the `a` prefix.

### Environment Variables
**PascalCase, no separators.** `TrSetupBaseUrl` NOT `TRSETUP_BASE_URL` and NOT `TrSetup__BaseUrl`. Use a custom configuration provider mapping PascalCase env vars → `:`-nested config paths. Read via `IConfiguration["Section:Key"]` only — never `Environment.GetEnvironmentVariable(...)`.

### File Structure
```csharp
using System;

namespace TrSetup.Services.Example;

public class DatabaseService
{
    private readonly ILogger<DatabaseService> objLogger;
    private readonly IConfiguration objConfiguration;

    public DatabaseService(ILogger<DatabaseService> aLogger, IConfiguration aConfiguration)
    {
        objLogger = aLogger;
        objConfiguration = aConfiguration;
    }

    public string ConnectionString { get; set; }

    public async Task<DataTable> GetDataAsync(string aQueryName)
    {
        var vConnString = objConfiguration.GetConnectionString("Default");
        var vResult = await ExecuteQueryAsync(vConnString, aQueryName);
        return vResult;
    }
}
```

### Best Practices
- One class per file. File name matches class.
- File-scoped namespaces. Nullable reference types enabled.
- Methods small (<20 lines). Single responsibility.
- Max 3 nesting levels. Early returns for validation.
- ConfigureAwait(false) in libraries.
- StringBuilder for loop concatenation. Dispose IDisposable. Cache expensive ops.

### XML Documentation (MANDATORY on public members)
`<summary>`, `<remarks>`, `<param>`, `<returns>`, `<exception>` — all required.

### Testing
- Short PascalCase test name, no underscores. Full scenario in XML `<summary>`.
- Arrange-Act-Assert. One assertion per test where practical.

### Security
- Never hardcode credentials. Parameterized queries. Validate inputs. Log security events.

### MAUI UI testability — stable AutomationId (MAUI apps only)
- Every interactive or data-bound control the verifier must reach (buttons, entries, pickers, list/collection views, key labels/values) carries a stable, unique **`AutomationId`** — the native analogue of a stable DOM id for Playwright. Without it Appium selectors drift and the runtime gates (`verify-phase §4a/§4b`) can't reliably find controls on the Android/iOS/Mac Catalyst heads.
- Name them by intent, not layout: `AutomationId="LoginSubmitButton"`, `AutomationId="ClientsGrid"`, `AutomationId="TotalBalanceValue"` — never positional (`Button2`).
- Set it on the control whose data the gate asserts (the grid/list itself, the value label), so "rows present AND non-empty" / "value not blank" maps to one addressable element.
- (Blazor screens use the equivalent `data-testid`/stable element ids for Playwright — same principle.)

## Enforcement

### .editorconfig (machine-checkable)
- File-scoped namespaces (`warning`)
- Async-method `Async` suffix (`warning`)
- `var` for locals (`warning`)
- Nullable reference types enabled
- No `_` prefix on private fields (`warning` via custom naming rule)

### Verifier grep checks
```bash
# Forbidden underscore-prefix fields
grep -rE "private(\s+readonly)?\s+\w+\s+_[a-z]" src/ source/ 2>/dev/null

# Forbidden test-method underscores
grep -rE "public\s+(async\s+)?Task\s+\w+_\w+\s*\(" tests/

# Field missing obj prefix
grep -rE "private(\s+readonly)?\s+\w+\s+(?!obj)[A-Z]\w+\s*[;=]" src/ source/ 2>/dev/null | grep -v "static\|const"
```

### Severity
- **Error**: file-scoped namespace, underscore field prefix
- **Warning**: nullable, async suffix
- **Info**: consider fixing
