FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Domus.Domain/Domus.Domain.csproj Domus.Domain/
COPY Domus.Application/Domus.Application.csproj Domus.Application/
COPY Domus.Infrastructure/Domus.Infrastructure.csproj Domus.Infrastructure/
COPY Domus.Api/Domus.Api.csproj Domus.Api/
RUN dotnet restore Domus.Api/Domus.Api.csproj

COPY . .
RUN dotnet publish Domus.Api/Domus.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=10 \
  CMD curl -fsS http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "Domus.Api.dll"]
