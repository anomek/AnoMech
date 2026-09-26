FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

ARG VERSION=0.0.0
WORKDIR /src

COPY Relay/AnoMech.Relay/AnoMech.Relay.csproj Relay/AnoMech.Relay/
RUN dotnet restore Relay/AnoMech.Relay/AnoMech.Relay.csproj

COPY Shared/ Shared/
COPY Relay/AnoMech.Relay/ Relay/AnoMech.Relay/
RUN dotnet publish Relay/AnoMech.Relay/AnoMech.Relay.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:UseAppHost=false \
    -p:Version=${VERSION}

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final

WORKDIR /app
COPY --from=build --chown=app:app /app/publish .

USER app
EXPOSE 7890

ENTRYPOINT ["dotnet", "AnoMech.Relay.dll"]
