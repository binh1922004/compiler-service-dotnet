#!/bin/bash
# run_and_measure.sh

LANGUAGE=$1
SUBMISSION_DIR=$2
INPUT_FILE=$3
TIMEOUT=$4
MEMORY_LIMIT_MB=$5

cd "$SUBMISSION_DIR" || exit 1

case $LANGUAGE in
    cpp)
#        g++ -std=c++17 -O2 "$SOURCE_FILE" -o Main 2>&1 || exit 1
        EXECUTABLE="./Main"
        ;;

    python)
#        python3 -m py_compile "$SOURCE_FILE" 2>&1 || exit 1
        EXECUTABLE="python3 Main.py"
        ;;
esac

# Use /usr/bin/time to measure
TIME_FORMAT='{"execTimeMs":%e,"peakMemoryKB":%M,"userTimeMs":%U,"sysTimeMs":%S,"exitCode":%x}'

STDOUT_FILE=$(mktemp)
STDERR_FILE=$(mktemp)
TIME_FILE=$(mktemp)

# Run with measurement
timeout "${TIMEOUT}s" /usr/bin/time -f "$TIME_FORMAT" -o "$TIME_FILE" \
    bash -c "ulimit -v \$((${MEMORY_LIMIT_MB}*1024)); $EXECUTABLE < ${INPUT_FILE}" \
    > "$STDOUT_FILE" 2> "$STDERR_FILE"

EXIT_CODE=$?

# Read stats
if [ -f "$TIME_FILE" ]; then
    STATS=$(cat "$TIME_FILE")
else
    STATS='{"execTimeMs":0,"peakMemoryKB":0}'
fi

# Determine status
if [ $EXIT_CODE -eq 124 ]; then
    STATUS="TLE"
elif [ $EXIT_CODE -ne 0 ]; then
    STATUS="RE"
else
    STATUS="AC"
fi

# Output JSON
jq -n \
    --arg status "$STATUS" \
    --argjson exitCode "$EXIT_CODE" \
    --rawfile stdout "$STDOUT_FILE" \
    --rawfile stderr "$STDERR_FILE" \
    --argjson stats "$STATS" \
    '{
        status: $status,
        exitCode: $exitCode,
        stdout: $stdout,
        stderr:  $stderr,
        stats: $stats
    }'

rm -f "$STDOUT_FILE" "$STDERR_FILE" "$TIME_FILE"