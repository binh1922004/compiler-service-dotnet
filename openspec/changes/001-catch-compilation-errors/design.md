# Design: Catch Compilation Errors

## Architecture

### Current Flow
```
User Code → Kafka → SubmissionHandler → CompileService.SubmitCode()
                                              ↓
                                         Exception caught
                                              ↓
                                         Returns null
                                              ↓
                                    SubmissionHandler sends IE
```

### Proposed Flow
```
User Code → Kafka → SubmissionHandler → CompileService.SubmitCode()
                                              ↓
                                    Judge script execution
                                              ↓
                                    Parse judge output for CE
                                              ↓
                         Return SubmissionResponse with error details
                                              ↓
                                    Send CE status via Kafka
```

## Key Changes

### 1. Enhance SubmissionResponse Model
Add an optional `Error` property to capture compilation error details:
```csharp
public string? Error { get; set; }
```

### 2. Modify CompileService.SubmitCode()
- Remove the blanket try-catch that returns null
- Let the judge script output be parsed
- Check if the response contains a CE status
- If CE, include the error message in the response
- Only catch truly exceptional cases (Docker failures, etc.)

### 3. Update SubmissionHandler
- Remove defensive null check (SubmitCode will always return a response)
- Still catch Docker/infrastructure exceptions and return IE

## Error Handling Strategy

| Scenario | Status | Error Field |
|----------|--------|-------------|
| Compilation failure | CE | Compiler error message |
| Runtime error in test | RE | Runtime details |
| Docker/infra failure | IE | null |
| Judge script failure | IE | null |

## Implementation Notes

- The judge.py script already outputs CE status in JSON
- We need to parse and preserve the error message from judge output
- Keep exception handling for Docker pool failures
- Don't swallow compilation errors with generic IE responses
