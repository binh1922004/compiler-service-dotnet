# Implementation Tasks

## Task 1: Update SubmissionResponse Model
**File:** `Models/SubmissionResponse.cs`

Add an optional `Error` property to capture error messages:
```csharp
[JsonPropertyName("error")]
public string? Error { get; set; }
```

**Acceptance Criteria:**
- Property is nullable to support successful submissions
- Uses camelCase JSON naming convention
- Serializes/deserializes correctly

---

## Task 2: Modify CompileService to Preserve Error Details
**File:** `Services/CompileService.cs`

**Changes in `SubmitCode()` method:**
1. Remove the try-catch that returns null on all exceptions
2. Parse the judge output even when it contains CE status
3. Add targeted exception handling only for Docker/infrastructure failures
4. Return a proper SubmissionResponse with CE status and error message

**Acceptance Criteria:**
- Judge script output is always parsed when available
- CE responses include the error message in the Error field
- Docker pool failures still return IE status
- No null returns (always return a SubmissionResponse)

---

## Task 3: Update SubmissionHandler Error Handling
**File:** `Infrastructure/Kafka/Handlers/SubmissionHandler.cs`

**Changes:**
1. Remove null check for result (no longer needed)
2. Keep exception catch for infrastructure failures
3. Ensure IE responses are only for true system failures

**Acceptance Criteria:**
- Always sends a response to Kafka
- CE errors from user code are sent with error details
- IE responses are reserved for infrastructure failures
- Exception logging includes full context

---

## Task 4: Test Compilation Error Scenarios
**File:** `Test/Services/CompilerServiceTests.cs` or create new test

Add test cases for:
1. Valid code → AC status
2. Code with syntax error → CE status with error message
3. Code with runtime error → RE status
4. Docker failure → IE status

**Acceptance Criteria:**
- All scenarios return proper status codes
- Error field is populated for CE responses
- Error field is null for successful submissions
