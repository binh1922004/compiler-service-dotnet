# Catch Compilation Errors and Send Back via Kafka

## What
Enhance the compiler service to properly catch compilation errors from user code and return them via Kafka with detailed error information.

## Why
Currently, when user code fails to compile:
- The `CompileService.SubmitCode()` method returns `null` on exceptions
- The `SubmissionHandler` catches this and sends back a generic "Internal Error" (IE) status
- **No actual compilation error details are captured or returned to the user**
- Users receive no feedback about what went wrong with their code

This makes debugging impossible for users since they don't know if their code has syntax errors, compilation failures, or other issues.

## Expected Outcome
- Compilation errors are properly caught and parsed from the judge script output
- The `SubmissionResponse` includes error details when compilation fails
- Users receive a `CE` (Compilation Error) status with the actual error message
- The Kafka response contains actionable information the user can act on

## Impact
- Better user experience with clear error messages
- Proper distinction between compilation errors (CE) and internal system errors (IE)
- Easier debugging for users submitting code
