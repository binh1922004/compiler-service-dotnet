# Sử dụng python:3.10-slim làm base (dựa trên Debian Bookworm hoặc Bullseye)
# Image này đã có sẵn Python 3.10 tối ưu
FROM python:3.10-slim

ENV DEBIAN_FRONTEND=noninteractive
ENV PYTHONUNBUFFERED=1

# Cài đặt các thư viện hệ thống cần thiết
# Thêm curl và ca-certificates để tải cài đặt Node.js
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    g++ \
    time \
    jq \
    bc \
    dos2unix \
    vim \
    ruby \
    curl \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Cài đặt Node.js (Phiên bản 20.x LTS)
# Gói nodejs từ NodeSource đã bao gồm sẵn npm
RUN curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && rm -rf /var/lib/apt/lists/*

# Cài đặt các thư viện Python
# Vì dùng slim (glibc), pip sẽ tải binary wheels, không cần compile lâu
RUN pip install --no-cache-dir --upgrade pip && \
    pip install --no-cache-dir \
        numpy scipy sympy sortedcontainers networkx

# Thiết lập user
RUN useradd -m -s /bin/bash judgeuser && \
    mkdir -p /work /scripts && \
    chown -R judgeuser:judgeuser /work

# Copy scripts
COPY judge.py /scripts/judge.py
COPY test_case_generator.py /scripts/test_case_generator.py
COPY runner.js /scripts/runner.js

WORKDIR /work
USER judgeuser

CMD ["/bin/bash"]