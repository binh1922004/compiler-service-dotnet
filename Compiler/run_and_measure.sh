#!/bin/bash
# run_and_measure.sh
# Executes a program and measures execution time, memory usage, and captures output

set -uo pipefail

# --- INPUT PARAMETERS ---
LANGUAGE=$1
SUBMISSION_DIR=$2
TESTCASE_DIR=$3        # Thay đổi: Đường dẫn đến thư mục chứa testcase
TIMEOUT=$4             # Thời gian giới hạn cho MỖI testcase
MEMORY_LIMIT_MB=$5     # Bộ nhớ giới hạn cho MỖI testcase
OUTPUT_LIMIT_KB=65536  # 64MB output limit

cd "$SUBMISSION_DIR" || exit 1

# --- SETUP EXECUTABLE ---
case $LANGUAGE in
    cpp)    EXECUTABLE="./Main" ;;
    python) EXECUTABLE="python3 Main.py" ;;
    java)   EXECUTABLE="java Main" ;;
    *)      echo '{"status":"ERR","error":"Unsupported language"}' >&2; exit 1 ;;
esac

# File tạm
STDOUT_FILE=$(mktemp)
STDERR_FILE=$(mktemp)
TIME_FILE=$(mktemp)
RESULTS_JSON=$(mktemp) # File chứa danh sách kết quả JSON

# Khởi tạo mảng JSON
echo "[" > "$RESULTS_JSON"
FIRST_ITEM=true

cleanup() {
    rm -f "$STDOUT_FILE" "$STDERR_FILE" "$TIME_FILE" "$RESULTS_JSON"
}
trap cleanup EXIT

# --- LOOP QUA CÁC FILE INPUT ---
# Sử dụng sort -V để đảm bảo thứ tự 1.in, 2.in, ... 10.in (thay vì 1, 10, 2)
for INPUT_FILE in $(ls "$TESTCASE_DIR"/inp/*.inp | sort -V); do
    
    # Lấy tên file base (ví dụ: 1.in -> 1)
    BASENAME=$(basename "$INPUT_FILE" .inp)
    EXPECTED_FILE="$TESTCASE_DIR/out/$BASENAME.out"

    # Reset biến cho mỗi vòng lặp
    STATUS="AC"
    EXEC_TIME_MS=0
    PEAK_MEMORY_KB=0
    
    # --- EXECUTION ---
    # Chạy code user với input hiện tại
    set +e
    TIME_FORMAT='{"execTimeSec":%e,"peakMemoryKB":%M,"exitCode":%x}'
    
    timeout "${TIMEOUT}s" /usr/bin/time -f "$TIME_FORMAT" -o "$TIME_FILE" \
        bash -c "ulimit -v \$((${MEMORY_LIMIT_MB}*1024)); ulimit -f ${OUTPUT_LIMIT_KB}; $EXECUTABLE < $INPUT_FILE" \
        > "$STDOUT_FILE" 2> "$STDERR_FILE"
        
    EXIT_CODE=$?
    set -e

    # --- PARSE STATS ---
    if [ -f "$TIME_FILE" ] && [ -s "$TIME_FILE" ] && cat "$TIME_FILE" | jq empty 2>/dev/null; then
        STATS_RAW=$(cat "$TIME_FILE")
        EXEC_TIME_MS=$(echo "$STATS_RAW" | jq -r '.execTimeSec * 1000 | round')
        PEAK_MEMORY_KB=$(echo "$STATS_RAW" | jq -r '.peakMemoryKB')
        USER_EXIT_CODE=$(echo "$STATS_RAW" | jq -r '.exitCode')
    else
        # Fallback nếu time command lỗi
        EXEC_TIME_MS=0
        PEAK_MEMORY_KB=0
        USER_EXIT_CODE=$EXIT_CODE
    fi

    # --- JUDGING LOGIC ---
    if [ $EXIT_CODE -eq 124 ]; then
        STATUS="TLE"
        EXEC_TIME_MS=$((TIMEOUT * 1000))
    elif [ $EXIT_CODE -eq 137 ] || [ $EXIT_CODE -eq 153 ]; then
        STATUS="MLE"
    elif [ $EXIT_CODE -ne 0 ]; then
        STATUS="RE"
        # Check MLE heuristics
        if [ "$PEAK_MEMORY_KB" -gt 0 ] && [ "$PEAK_MEMORY_KB" -ge $((MEMORY_LIMIT_MB * 1024)) ]; then
            STATUS="MLE"
        fi
    fi

    # --- COMPARE OUTPUT (Token-based) ---
    if [ "$STATUS" == "AC" ]; then
        if [ ! -f "$EXPECTED_FILE" ]; then
            STATUS="ERR" # Lỗi hệ thống: Không tìm thấy file đáp án
        else
            # So sánh dùng tr + sed như đã bàn
            if diff -q <(tr -s '[:space:]' '\n' < "$STDOUT_FILE" | sed '/^$/d') \
                       <(tr -s '[:space:]' '\n' < "$EXPECTED_FILE" | sed '/^$/d') > /dev/null; then
                STATUS="AC"
            else
                STATUS="WA"
            fi
        fi
    fi

    # --- APPEND RESULT TO JSON ---
    # Thêm dấu phẩy nếu không phải item đầu tiên
    if [ "$FIRST_ITEM" = true ]; then
        FIRST_ITEM=false
    else
        echo "," >> "$RESULTS_JSON"
    fi

    # Tạo JSON object cho testcase này
    # Lưu ý: Không lưu stdout/stderr để tiết kiệm, chỉ lưu status
    jq -n -c \
        --arg case "$BASENAME" \
        --arg status "$STATUS" \
        --argjson time "$EXEC_TIME_MS" \
        --argjson memory "$PEAK_MEMORY_KB" \
        '{testCase: $case, status: $status, time: $time, memory: $memory}' >> "$RESULTS_JSON"

done

# Đóng mảng JSON
echo "]" >> "$RESULTS_JSON"

# --- OUTPUT FINAL JSON ---
cat "$RESULTS_JSON"