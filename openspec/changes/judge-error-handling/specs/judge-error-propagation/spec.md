## ADDED Requirements

### Requirement: Judge script SHALL always produce valid JSON output
The `judge.py` script SHALL always output a valid JSON object to stdout, even when the script encounters an unhandled exception. The JSON object MUST contain at minimum a `status` field and an `error` field when a failure occurs.

#### Scenario: Unhandled Python exception in judge.py
- **WHEN** `judge.py` encounters an unhandled Python exception (e.g., ImportError, PermissionError, unexpected crash)
- **THEN** the script SHALL output a JSON object to stdout with `status: "ERROR"` and `error` containing the exception type and message, and `traceback` containing the full Python traceback

#### Scenario: Normal execution continues to work
- **WHEN** `judge.py` runs a valid solution against valid test cases
- **THEN** the script SHALL output the same JSON format as before (status, passed, total, max_time, max_memory_mb)

### Requirement: C# service SHALL capture stderr from judge execution
The `CompileService.JudgeCode` method SHALL capture both stdout and stderr from the Docker container execution when running `judge.py`.

#### Scenario: Judge execution with stderr output
- **WHEN** the judge script produces output on both stdout and stderr
- **THEN** the service SHALL capture both streams and use stderr for logging/error context

#### Scenario: Judge execution with empty stdout but stderr present
- **WHEN** the judge script produces no stdout but has stderr output (e.g., Python crash before try/except wrapper)
- **THEN** the service SHALL return a `SubmissionResponse` with `Status = "IE"` and `Error` containing the stderr content

### Requirement: Error status from judge SHALL be mapped to SubmissionResponse
When `judge.py` returns a JSON object with `status: "ERROR"`, the C# service SHALL map this to a `SubmissionResponse` with `Status = "IE"` and populate the `Error` field with the error details from the JSON.

#### Scenario: Judge returns ERROR status with error details
- **WHEN** `judge.py` returns `{"status": "ERROR", "error": "some error message"}`
- **THEN** the service SHALL create a `SubmissionResponse` with `Status = "IE"`, `Error = "some error message"`, and `Passed = 0`, `Total = 0`

#### Scenario: Judge returns unparseable output
- **WHEN** the judge output cannot be parsed as JSON
- **THEN** the service SHALL create a `SubmissionResponse` with `Status = "IE"` and `Error` containing both the raw stdout and stderr for debugging
