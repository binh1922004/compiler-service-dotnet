# Sử dụng python:3.10-slim làm base (dựa trên Debian Bookworm hoặc Bullseye)
# Image này đã có sẵn Python 3.10 tối ưu
FROM python:3.10-slim

ENV DEBIAN_FRONTEND=noninteractive
ENV PYTHONUNBUFFERED=1

# Cài đặt các thư viện hệ thống cần thiết
# Lưu ý: python3 và pip đã có sẵn, không cần cài lại
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    g++ \
    time \
    jq \
    bc \
    dos2unix \
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
COPY run_and_measure.sh /scripts/run_and_measure.sh
COPY wrapper.cpp wrapper.cpp

# Chuyển đổi định dạng dòng (đề phòng file từ Windows) và phân quyền
RUN dos2unix /scripts/run_and_measure.sh && \
    chmod +x /scripts/*.sh && \
    chmod +x wrapper.cpp

WORKDIR /work
USER judgeuser

CMD ["/bin/bash"]