# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet publish src/DotSearch/DotSearch.csproj \
    -c Release \
    -o /app/publish \
    --self-contained false \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV DOTSEARCH_DATA_DIR=/data \
    DOTSEARCH_PORT=5280 \
    ASPNETCORE_URLS=http://+:5280

RUN mkdir -p /data
COPY --from=build /app/publish .

VOLUME ["/data"]
EXPOSE 5280

ENTRYPOINT ["dotnet", "DotSearch.dll"]
