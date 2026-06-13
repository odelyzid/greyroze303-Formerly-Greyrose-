# Stage 1: Build — compile and publish for all target platforms
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project and restore (leveraging Docker cache)
COPY *.csproj .
RUN dotnet restore

# Copy all source files
COPY . .

# Publish for each RID (self-contained single-file)
RUN dotnet publish -c Release -r win-x64      --self-contained true -p:PublishSingleFile=true -p:DebugType=embedded -o /out/win-x64
RUN dotnet publish -c Release -r linux-x64     --self-contained true -p:PublishSingleFile=true -p:DebugType=embedded -o /out/linux-x64
RUN dotnet publish -c Release -r linux-arm     --self-contained true -p:PublishSingleFile=true -p:DebugType=embedded -o /out/linux-arm
RUN dotnet publish -c Release -r linux-arm64   --self-contained true -p:PublishSingleFile=true -p:DebugType=embedded -o /out/linux-arm64

# Stage 2: Runtime image — minimal deployment for running in Docker
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /out/linux-x64/Greyrose .
EXPOSE 12000 12170 12171 12500
ENTRYPOINT ["./Greyrose"]

# Stage 3: Export layer — used by build.sh to extract artifacts via docker run
FROM alpine:3.20 AS export
COPY --from=build /out /out
