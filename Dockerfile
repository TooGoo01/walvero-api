# syntax=docker/dockerfile:1.6
# Multi-stage build: .NET 8 + ffmpeg üçün

# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# csproj-ları əvvəl kopyala — restore cache layer
COPY ["Loyalty.WebAPI/Loyalty.WebAPI.csproj", "Loyalty.WebAPI/"]
COPY ["Loyalty.Business/Loyalty.Business.csproj", "Loyalty.Business/"]
COPY ["Loyalty.DataAccess/Loyalty.DataAccess.csproj", "Loyalty.DataAccess/"]
COPY ["Loyalty.Infrastructure/Loyalty.Infrastructure.csproj", "Loyalty.Infrastructure/"]
RUN dotnet restore "Loyalty.WebAPI/Loyalty.WebAPI.csproj"

# Qalan source-u kopyala və publish et
COPY . .
WORKDIR /src/Loyalty.WebAPI
RUN dotnet publish "Loyalty.WebAPI.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---------- Runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

# ffmpeg (HLS encoding) + tesseract-ocr (Receipt OCR) quraşdır
# `--no-install-recommends` ilə kiçik image
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ffmpeg \
        tesseract-ocr \
        tesseract-ocr-aze \
        tesseract-ocr-eng \
        libtesseract-dev \
        libleptonica-dev \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Tesseract NuGet 5.2.0 literal lib adı axtarır (`libleptonica-1.82.0.so`, `libtesseract50.so`).
# apt fərqli SONAME quraşdırır (`liblept.so.5`, `libtesseract.so.5`). Symlink ilə bağlayırıq.
RUN ln -sf /usr/lib/x86_64-linux-gnu/liblept.so.5 /usr/lib/x86_64-linux-gnu/libleptonica-1.82.0.so \
    && ln -sf /usr/lib/x86_64-linux-gnu/libtesseract.so.5 /usr/lib/x86_64-linux-gnu/libtesseract50.so

# Yoxlama (build zamanı log-da görünür)
RUN ffmpeg -version | head -1 && ffprobe -version | head -1 && tesseract --version 2>&1 | head -1 && tesseract --list-langs && ls -la /usr/lib/x86_64-linux-gnu/libleptonica-1.82.0.so /usr/lib/x86_64-linux-gnu/libtesseract50.so

WORKDIR /app
COPY --from=build /app/publish .

# Azure App Service container default port 8080 dinləyir
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Loyalty.WebAPI.dll"]
