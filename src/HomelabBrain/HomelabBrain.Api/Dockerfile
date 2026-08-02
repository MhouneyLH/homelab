# syntax=docker/dockerfile:1
# Build context must be src/HomelabBrain/ (not this directory) - HomelabBrain.Api references
# sibling projects. From repo root: docker build -f HomelabBrain.Api/Dockerfile src/HomelabBrain

FROM mcr.microsoft.com/dotnet/sdk:11.0-preview AS build
WORKDIR /src

# Restore first, with only the files that affect it - so this layer stays cached across
# source-only changes and doesn't get invalidated on every edit. RestoreLockedMode=true
# (Directory.Build.props) needs each project's packages.lock.json too, not just its csproj.
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY HomelabBrain.Api/HomelabBrain.Api.csproj HomelabBrain.Api/packages.lock.json HomelabBrain.Api/
COPY HomelabBrain.ServiceDefaults/HomelabBrain.ServiceDefaults.csproj HomelabBrain.ServiceDefaults/packages.lock.json HomelabBrain.ServiceDefaults/
COPY HomelabBrain.PlantAnalyzer/HomelabBrain.PlantAnalyzer.csproj HomelabBrain.PlantAnalyzer/packages.lock.json HomelabBrain.PlantAnalyzer/
COPY HomelabBrain.DeviceConfig/HomelabBrain.DeviceConfig.csproj HomelabBrain.DeviceConfig/packages.lock.json HomelabBrain.DeviceConfig/

RUN dotnet restore HomelabBrain.Api/HomelabBrain.Api.csproj

COPY HomelabBrain.Api/ HomelabBrain.Api/
COPY HomelabBrain.ServiceDefaults/ HomelabBrain.ServiceDefaults/
COPY HomelabBrain.PlantAnalyzer/ HomelabBrain.PlantAnalyzer/
COPY HomelabBrain.DeviceConfig/ HomelabBrain.DeviceConfig/

RUN dotnet publish HomelabBrain.Api/HomelabBrain.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:11.0-preview AS runtime
WORKDIR /app

# APP_UID is the base image's built-in non-root user (Microsoft's recommended pattern for
# .NET container images) - no need to create one manually.
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "HomelabBrain.Api.dll"]
