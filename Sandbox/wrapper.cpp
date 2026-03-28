#include <iostream>
#include <fstream>
#include <sstream>
#include <chrono>
#include <cstring>
#include <unistd.h>

class ProgramStats {
private:
    std::chrono::high_resolution_clock::time_point startTime;
    long long peakMemoryKB;
    long long currentMemoryKB;

public:
    ProgramStats() {
        startTime = std::chrono::high_resolution_clock::now();
        peakMemoryKB = 0;
        currentMemoryKB = 0;
    }

    // 🔴 Lấy peak memory (KB)
    long long getPeakMemoryKB() {
        std::ifstream status("/proc/self/status");
        std::string line;

        while (std::getline(status, line)) {
            if (line.find("VmPeak:") == 0) {
                std::istringstream iss(line);
                std::string label;
                long long value;
                iss >> label >> value;
                return value;
            }
        }
        return 0;
    }

    // 🔴 Lấy current memory (KB)
    long long getCurrentMemoryKB() {
        std::ifstream status("/proc/self/status");
        std::string line;

        while (std::getline(status, line)) {
            if (line.find("VmRSS:") == 0) {
                std::istringstream iss(line);
                std::string label;
                long long value;
                iss >> label >> value;
                return value;
            }
        }
        return 0;
    }

    // 🔴 Lấy execution time (ms)
    long long getExecutionTimeMs() {
        auto endTime = std::chrono::high_resolution_clock::now();
        auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(endTime - startTime);
        return duration.count();
    }

    // 🔴 Convert KB to MB
    double toMB(long long kb) {
        return kb / 1024.0;
    }

    // 🔴 Print stats to stderr (dưới dạng JSON để dễ parse)
    void printStats() {
        long long execTimeMs = getExecutionTimeMs();
        long long peakKB = getPeakMemoryKB();
        long long currentKB = getCurrentMemoryKB();
        double peakMB = toMB(peakKB);
        double currentMB = toMB(currentKB);

        // Output JSON to stderr
        fprintf(stderr, "{\"execTimeMs\": %lld, \"peakMemoryKB\": %lld, \"peakMemoryMB\": %.2f, \"currentMemoryKB\": %lld, \"currentMemoryMB\": %.2f}\n",
                execTimeMs, peakKB, peakMB, currentKB, currentMB);
    }

    // 🔴 Destructor - tự động in stats khi program exit
    ~ProgramStats() {
        printStats();
    }
};

// Global instance - tự động track từ khi program start đến khi exit
static ProgramStats __stats;

// 🔴 Optional: Function để user gọi nếu cần ok
void printProgramStats() {
    __stats.printStats();
}

