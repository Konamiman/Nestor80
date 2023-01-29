# syntax=docker/dockerfile:1

ARG BUILD_OS=linux \
    BUILD_ARCH=x64 \
    DOTNET_VERSION=6.0
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} as build-env

WORKDIR /src
COPY . .

ENV PUBLISH_PROFILE="FrameworkDependant__${BUILD_OS}_${BUILD_ARCH}"

RUN dotnet publish Nestor80.sln /p:PublishProfile=${PUBLISH_PROFILE} /p:DebugType=None -c Release

# ----

# Cannot use alpine since uses musl instead of linux-c.
FROM mcr.microsoft.com/dotnet/runtime:${DOTNET_VERSION}-bullseye-slim

COPY --from=build-env /src/N80/bin/Release/net6.0/publish/* /bin

ENTRYPOINT ["/bin/N80"]
